using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using NovelMoyo.Models;

namespace NovelMoyo.Services.BookSource;

public class BookSourceParser
{
    private readonly HttpClient _httpClient;

    public BookSourceParser(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SearchResult>> SearchAsync(Models.BookSource source, string keyword)
    {
        var encodedKeyword = Uri.EscapeDataString(keyword);
        string response;

        if (source.Search.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            var url = source.Search.Url;
            var paramName = string.IsNullOrEmpty(source.Search.Params) ? "s" : source.Search.Params;
            var formData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [paramName] = keyword
            });
            var postResponse = await _httpClient.PostAsync(url, formData);
            postResponse.EnsureSuccessStatusCode();
            var encoding = source.Charset.ToLower() switch
            {
                "gbk" => Encoding.GetEncoding("GBK"),
                "gb2312" => Encoding.GetEncoding("GB2312"),
                _ => Encoding.UTF8
            };
            var bytes = await postResponse.Content.ReadAsByteArrayAsync();
            response = encoding.GetString(bytes);
        }
        else
        {
            var url = source.Search.Url.Replace("{keyword}", encodedKeyword);
            response = await FetchPageAsync(url, source.Charset);
        }

        if (source.Search.ResultType == "json")
            return ParseSearchResultsJson(response, source);
        else
            return ParseSearchResultsHtml(response, source);
    }

    public async Task<int> GetTotalChaptersAsync(Models.BookSource source, string bookId)
    {
        try
        {
            // For API sources, use the book info endpoint
            // For HTML sources, we can't easily get chapter count without fetching the catalog
            if (string.IsNullOrEmpty(source.Catalog?.UrlTemplate))
                return 0;

            var url = $"{source.Host.TrimEnd('/')}/api/book?id={Uri.EscapeDataString(bookId)}";
            var response = await FetchPageAsync(url, source.Charset);
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("lastchapterid", out var lastChapterId))
            {
                if (lastChapterId.ValueKind == JsonValueKind.String &&
                    int.TryParse(lastChapterId.GetString(), out var count))
                    return count;
                if (lastChapterId.ValueKind == JsonValueKind.Number)
                    return lastChapterId.GetInt32();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetTotalChaptersAsync failed: {ex.Message}");
        }
        return 0;
    }

    public async Task<List<ChapterInfo>> GetCatalogAsync(Models.BookSource source, string bookId)
    {
        string url;
        if (!string.IsNullOrEmpty(source.Catalog.UrlTemplate))
        {
            url = source.Catalog.UrlTemplate
                .Replace("{bookId}", bookId)
                .Replace("{host}", source.Host);
        }
        else
        {
            url = bookId; // bookId is actually the full URL for HTML sources
        }

        var response = await FetchPageAsync(url, source.Charset);

        if (source.Catalog.ResultType == "json")
            return ParseCatalogJson(response, source);
        else
            return ParseCatalogHtml(response, source);
    }

    public async Task<(string title, string content)> GetContentAsync(Models.BookSource source, string bookId, int chapterId)
    {
        string url;
        if (!string.IsNullOrEmpty(source.Content.UrlTemplate))
        {
            url = source.Content.UrlTemplate
                .Replace("{bookId}", bookId)
                .Replace("{chapterId}", chapterId.ToString())
                .Replace("{host}", source.Host);
        }
        else
        {
            url = bookId; // bookId is actually the full URL for HTML sources
        }

        var response = await FetchPageAsync(url, source.Charset);

        string title, content;
        if (source.Content.ResultType == "json")
            (title, content) = ParseContentJson(response, source);
        else
            (title, content) = ParseContentHtml(response, source);

        // Apply content filter
        if (source.Content.Filter?.Type == "remove_keywords")
        {
            foreach (var keyword in source.Content.Filter.Keywords)
            {
                content = content.Replace(keyword, string.Empty);
            }
        }

        return (title, content.Trim());
    }

    private async Task<string> FetchPageAsync(string url, string charset)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var encoding = charset.ToLower() switch
        {
            "gbk" => Encoding.GetEncoding("GBK"),
            "gb2312" => Encoding.GetEncoding("GB2312"),
            _ => Encoding.UTF8
        };

        var bytes = await response.Content.ReadAsByteArrayAsync();
        return encoding.GetString(bytes);
    }

    private List<SearchResult> ParseSearchResultsHtml(string html, Models.BookSource source)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<SearchResult>();
        var selectors = source.Search.Selectors;
        if (selectors == null) return results;

        var items = doc.DocumentNode.SelectNodes(selectors.Item);
        if (items == null) return results;

        foreach (var item in items)
        {
            var nameNode = selectors.Name != null ? item.SelectSingleNode(selectors.Name) : null;
            var authorNode = selectors.Author != null ? item.SelectSingleNode(selectors.Author) : null;
            var urlNode = selectors.Url != null ? item.SelectSingleNode(selectors.Url) : null;
            var latestNode = selectors.LatestChapter != null ? item.SelectSingleNode(selectors.LatestChapter) : null;

            var bookUrl = urlNode?.GetAttributeValue("href", "") ?? "";
            if (!string.IsNullOrEmpty(bookUrl) && !bookUrl.StartsWith("http"))
            {
                bookUrl = source.Host.TrimEnd('/') + "/" + bookUrl.TrimStart('/');
            }

            results.Add(new SearchResult
            {
                Title = nameNode?.InnerText?.Trim() ?? "",
                Author = CleanText(authorNode?.InnerText ?? ""),
                Url = bookUrl,
                LatestChapter = latestNode?.InnerText?.Trim() ?? "",
                SourceName = source.Title,
                Source = source
            });
        }

        return results;
    }

    private List<SearchResult> ParseSearchResultsJson(string json, Models.BookSource source)
    {
        var results = new List<SearchResult>();
        var jsonPath = source.Search.JsonPath;
        if (jsonPath == null) return results;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var items = ResolveJsonPath(doc.RootElement, jsonPath.Item);
            if (items is not { ValueKind: JsonValueKind.Array }) return results;

            foreach (var item in items.Value.EnumerateArray())
            {
                var bookUrl = ResolveJsonString(item, jsonPath.Url ?? "") ?? "";
                // For HTML sources, construct full URL from relative path
                // For API sources, keep the ID as-is
                if (source.Search.ResultType != "json" &&
                    !string.IsNullOrEmpty(bookUrl) && !bookUrl.StartsWith("http"))
                {
                    bookUrl = source.Host.TrimEnd('/') + "/" + bookUrl.TrimStart('/');
                }

                results.Add(new SearchResult
                {
                    Title = ResolveJsonString(item, jsonPath.Name ?? "") ?? "",
                    Author = ResolveJsonString(item, jsonPath.Author ?? "") ?? "",
                    Url = bookUrl,
                    LatestChapter = ResolveJsonString(item, jsonPath.LatestChapter ?? "") ?? "",
                    SourceName = source.Title,
                    Source = source
                });
            }
        }
        catch
        {
            // JSON parsing failed
        }

        return results;
    }

    private List<ChapterInfo> ParseCatalogHtml(string html, Models.BookSource source)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var chapters = new List<ChapterInfo>();
        var selectors = source.Catalog.Selectors;
        if (selectors == null) return chapters;

        var items = doc.DocumentNode.SelectNodes(selectors.Item);
        if (items == null) return chapters;

        var index = 0;
        foreach (var item in items)
        {
            var titleNode = selectors.Title != null ? item.SelectSingleNode(selectors.Title) : item;
            var urlNode = selectors.Url != null ? item.SelectSingleNode(selectors.Url) : null;

            var chapterUrl = urlNode?.GetAttributeValue("href", "") ?? "";
            if (!string.IsNullOrEmpty(chapterUrl) && !chapterUrl.StartsWith("http"))
            {
                chapterUrl = source.Host.TrimEnd('/') + "/" + chapterUrl.TrimStart('/');
            }

            chapters.Add(new ChapterInfo
            {
                Index = index++,
                Title = titleNode?.InnerText?.Trim() ?? "",
                Url = chapterUrl
            });
        }

        return chapters;
    }

    private List<ChapterInfo> ParseCatalogJson(string json, Models.BookSource source)
    {
        var chapters = new List<ChapterInfo>();
        var jsonPath = source.Catalog.JsonPath;
        if (jsonPath == null) return chapters;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var items = ResolveJsonPath(doc.RootElement, jsonPath.Item);
            if (items is not { ValueKind: JsonValueKind.Array }) return chapters;

            var index = 0;
            foreach (var item in items.Value.EnumerateArray())
            {
                var chapterUrl = ResolveJsonString(item, jsonPath.Url ?? "") ?? "";
                if (!string.IsNullOrEmpty(chapterUrl) && !chapterUrl.StartsWith("http"))
                {
                    chapterUrl = source.Host.TrimEnd('/') + "/" + chapterUrl.TrimStart('/');
                }

                chapters.Add(new ChapterInfo
                {
                    Index = index++,
                    Title = ResolveJsonString(item, jsonPath.Title ?? "") ?? "",
                    Url = chapterUrl
                });
            }
        }
        catch
        {
            // JSON parsing failed
        }

        return chapters;
    }

    private (string title, string content) ParseContentHtml(string html, Models.BookSource source)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var selectors = source.Content.Selectors;
        if (selectors == null) return ("", "");

        var titleNode = selectors.Title != null ? doc.DocumentNode.SelectSingleNode(selectors.Title) : null;
        var bodyNode = selectors.Body != null ? doc.DocumentNode.SelectSingleNode(selectors.Body) : null;

        var title = titleNode?.InnerText?.Trim() ?? "";
        var content = bodyNode?.InnerText?.Trim() ?? "";

        // Clean up HTML whitespace
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\s*\n\s*", "\n");
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\n{3,}", "\n\n");

        return (title, content);
    }

    private (string title, string content) ParseContentJson(string json, Models.BookSource source)
    {
        var jsonPath = source.Content.JsonPath;
        if (jsonPath == null) return ("", "");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var title = ResolveJsonString(doc.RootElement, jsonPath.Title ?? "") ?? "";
            var body = ResolveJsonString(doc.RootElement, jsonPath.Body ?? "") ?? "";
            return (title, body);
        }
        catch
        {
            return ("", "");
        }
    }

    private static JsonElement? ResolveJsonPath(JsonElement root, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // Simple JSON Path implementation: $.data.books[*] or $.data[*]
        var parts = path.TrimStart('$').Trim('.').Split('.');
        JsonElement current = root;

        foreach (var part in parts)
        {
            if (part.EndsWith("[*]"))
            {
                var key = part[..^3];
                if (!string.IsNullOrEmpty(key))
                {
                    if (current.ValueKind == JsonValueKind.Object &&
                        current.TryGetProperty(key, out var prop))
                    {
                        current = prop;
                    }
                    else return null;
                }
                // Return the array itself for enumeration
                if (current.ValueKind == JsonValueKind.Array)
                    return current;
                return null;
            }
            else
            {
                if (current.ValueKind == JsonValueKind.Object &&
                    current.TryGetProperty(part, out var prop))
                {
                    current = prop;
                }
                else return null;
            }
        }

        return current;
    }

    private static string? ResolveJsonString(JsonElement element, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // Handle "$" path - means the element itself
        if (path == "$")
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                _ => element.ToString()
            };
        }

        // For simple property access like $.book_name
        var parts = path.TrimStart('$').Trim('.').Split('.');
        JsonElement current = element;

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            if (current.ValueKind == JsonValueKind.Object &&
                current.TryGetProperty(part, out var prop))
            {
                current = prop;
            }
            else return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            _ => current.ToString()
        };
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        // Remove common prefixes like "作者："
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^作者[：:]\s*", "");
        return text.Trim();
    }
}

public class ChapterInfo
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
