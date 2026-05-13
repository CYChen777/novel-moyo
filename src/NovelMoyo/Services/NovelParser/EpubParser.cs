using System.IO;
using System.Text.RegularExpressions;
using NovelMoyo.Models;
using VersOne.Epub;

namespace NovelMoyo.Services.NovelParser;

public class EpubParser : INovelParser
{
    public Novel Parse(string filePath)
    {
        try
        {
            var epubBook = EpubReader.ReadBook(filePath);

            var title = epubBook.Title ?? Path.GetFileNameWithoutExtension(filePath);
            var chapters = ExtractChapters(epubBook);

            return new Novel
            {
                Title = title,
                FilePath = filePath,
                Format = "epub",
                Chapters = chapters
            };
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"无法解析 EPUB 文件: {ex.Message}", ex);
        }
    }

    private static List<Chapter> ExtractChapters(EpubBook epubBook)
    {
        var chapters = new List<Chapter>();
        var charOffset = 0;

        var readingOrder = epubBook.ReadingOrder;
        if (readingOrder is null || readingOrder.Count == 0)
            return chapters;

        for (var i = 0; i < readingOrder.Count; i++)
        {
            var contentFile = readingOrder[i];
            if (contentFile is null) continue;
            var content = contentFile.Content;
            if (content is null) continue;

            var plainText = HtmlToPlainText(content);

            if (string.IsNullOrWhiteSpace(plainText))
                continue;

            var title = ExtractTitle(plainText) ?? $"Chapter {i + 1}";

            chapters.Add(new Chapter
            {
                Index = chapters.Count,
                Title = title,
                Content = plainText,
                CharOffset = charOffset
            });

            charOffset += plainText.Length;
        }

        return chapters;
    }

    private static string HtmlToPlainText(string html)
    {
        var text = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</p>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static string? ExtractTitle(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return null;
        var firstLine = lines[0];
        return firstLine.Length <= 50 ? firstLine : null;
    }
}
