using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using NovelMoyo.Models;

namespace NovelMoyo.Services.BookSource;

public class SearchService : IDisposable
{
    private readonly BookSourceService _sourceService;
    private readonly BookSourceParser _parser;
    private readonly HttpClient _httpClient;
    private readonly SocketsHttpHandler _handler;

    public SearchService(BookSourceService sourceService)
    {
        _sourceService = sourceService;
        _handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }
        };
        _httpClient = new HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _parser = new BookSourceParser(_httpClient);
    }

    public async Task<List<SearchResult>> SearchAsync(string keyword)
    {
        var sources = _sourceService.GetEnabled();
        var tasks = sources.Select(source => SearchSingleSourceAsync(source, keyword));
        var results = await Task.WhenAll(tasks);
        var allResults = results.SelectMany(r => r).ToList();

        // Deduplicate by normalized title+author
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<SearchResult>();
        foreach (var result in allResults)
        {
            var key = $"{result.Title.Trim()}|{result.Author.Trim()}";
            if (seen.Add(key))
                deduped.Add(result);
        }

        // Fetch total chapters in parallel with concurrency limit
        using var chapterSemaphore = new System.Threading.SemaphoreSlim(5, 5);
        var chapterTasks = deduped.Select(async result =>
        {
            await chapterSemaphore.WaitAsync();
            try
            {
                result.TotalChapters = await _parser.GetTotalChaptersAsync(result.Source, result.Url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get chapters for {result.Title}: {ex.Message}");
            }
            finally
            {
                chapterSemaphore.Release();
            }
        });
        await Task.WhenAll(chapterTasks);

        return deduped;
    }

    private async Task<List<SearchResult>> SearchSingleSourceAsync(Models.BookSource source, string keyword)
    {
        const int maxRetries = 2;
        for (int retry = 0; retry <= maxRetries; retry++)
        {
            try
            {
                return await _parser.SearchAsync(source, keyword);
            }
            catch (HttpRequestException) when (retry < maxRetries)
            {
                await Task.Delay(500 * (retry + 1));
            }
            catch
            {
                return [];
            }
        }
        return [];
    }

    public async Task<List<SearchResult>> SearchByUrlAsync(string url)
    {
        var sources = _sourceService.GetEnabled();
        var matchedSource = sources.FirstOrDefault(s =>
            url.StartsWith(s.Host, StringComparison.OrdinalIgnoreCase));

        if (matchedSource == null)
            return [];

        try
        {
            // For API sources, extract bookId from URL
            // e.g., https://5084.bqg192.cc/#/book/717/ → 717
            var bookId = url;
            if (!string.IsNullOrEmpty(matchedSource.Catalog.UrlTemplate))
            {
                var match = System.Text.RegularExpressions.Regex.Match(url, @"/book/(\d+)");
                if (match.Success)
                    bookId = match.Groups[1].Value;
            }

            // Try to get the catalog page directly
            var catalog = await _parser.GetCatalogAsync(matchedSource, bookId);
            if (catalog.Count > 0)
            {
                return
                [
                    new SearchResult
                    {
                        Title = "从URL解析",
                        Url = bookId,
                        LatestChapter = $"共{catalog.Count}章",
                        SourceName = matchedSource.Title,
                        Source = matchedSource
                    }
                ];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SearchByUrlAsync failed: {ex.Message}");
        }

        return [];
    }

    public BookSourceParser GetParser() => _parser;

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}
