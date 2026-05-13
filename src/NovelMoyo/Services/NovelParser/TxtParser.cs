using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NovelMoyo.Models;

namespace NovelMoyo.Services.NovelParser;

public partial class TxtParser : INovelParser
{
    // Matches common Chinese chapter patterns:
    //   第123章, 第一二三章, 第1章 标题, Chapter 1, CHAPTER 12, etc.
    [GeneratedRegex(
        @"^(?:第[一二三四五六七八九十百千零\d]+章|Chapter\s+\d+|CHAPTER\s+\d+)",
        RegexOptions.Multiline)]
    private static partial Regex ChapterHeaderPattern();

    public Novel Parse(string filePath)
    {
        var text = ReadFileWithEncodingDetection(filePath);
        var chapters = SplitIntoChapters(text);
        var title = Path.GetFileNameWithoutExtension(filePath);

        return new Novel
        {
            Title = title,
            FilePath = filePath,
            Format = "txt",
            Chapters = chapters
        };
    }

    private static string ReadFileWithEncodingDetection(string filePath)
    {
        // Try UTF-8 first (with BOM detection)
        try
        {
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            using var reader = new StreamReader(filePath, utf8NoBom, detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            // Verify the decoded text is plausible CJK / Unicode — not mojibake
            if (HasReasonableCjkContent(text))
                return text;
        }
        catch (DecoderFallbackException)
        {
            // Fall through to UTF-16
        }

        // Try UTF-16 LE / BE (with BOM detection)
        try
        {
            using var reader = new StreamReader(filePath, Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            // Fall through to GBK
        }

        // Fallback to GBK/GB2312 (provider registered at app startup)
        try
        {
            var gbk = Encoding.GetEncoding("GBK");
            return File.ReadAllText(filePath, gbk);
        }
        catch
        {
            // Last resort: read as default with replacement
            return File.ReadAllText(filePath, Encoding.Default);
        }
    }

    /// <summary>
    /// Check if decoded text contains a reasonable proportion of CJK or common Unicode
    /// characters. If the text is mojibake (GBK misread as UTF-8), it will contain
    /// many characters in the Unicode "replacement" or "private use" ranges.
    /// </summary>
    private static bool HasReasonableCjkContent(string text)
    {
        if (text.Length == 0) return true;
        var validCount = 0;
        var sampleSize = Math.Min(text.Length, 500);
        for (int i = 0; i < sampleSize; i++)
        {
            var c = text[i];
            // Common CJK ranges, ASCII, punctuation, common symbols
            if (c >= 0x4E00 && c <= 0x9FFF   // CJK Unified Ideographs
                || c >= 0x3000 && c <= 0x303F // CJK Symbols and Punctuation
                || c >= 0xFF00 && c <= 0xFFEF  // Halfwidth and Fullwidth Forms
                || c < 0x80                     // ASCII
                || c >= 0x2000 && c <= 0x206F  // General Punctuation
                || char.IsWhiteSpace(c))
            {
                validCount++;
            }
        }
        // Require at least 80% of sampled characters to be in valid ranges
        return validCount >= sampleSize * 0.8;
    }

    private static List<Chapter> SplitIntoChapters(string text)
    {
        var pattern = ChapterHeaderPattern();
        var matches = pattern.Matches(text);

        if (matches.Count == 0)
        {
            // No chapters found -- treat entire text as one chapter
            return
            [
                new Chapter
                {
                    Index = 0,
                    Title = "全文",
                    Content = text.Trim(),
                    CharOffset = 0
                }
            ];
        }

        var chapters = new List<Chapter>();

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var startIndex = match.Index;

            // Content goes from end of this header to start of next header (or end of text)
            var contentStart = startIndex + match.Length;
            var contentEnd = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var content = text[contentStart..contentEnd].Trim();

            // Extract the full header line (marker + title text, e.g. "第一章 奇怪的梦")
            var lineEnd = text.IndexOf('\n', startIndex);
            if (lineEnd < 0) lineEnd = text.Length;
            var title = text[startIndex..lineEnd].Trim();

            chapters.Add(new Chapter
            {
                Index = i,
                Title = title,
                Content = content,
                CharOffset = startIndex
            });
        }

        // If there's text before the first chapter header, add it as a prologue
        if (matches[0].Index > 0)
        {
            var prologue = text[..matches[0].Index].Trim();
            if (!string.IsNullOrWhiteSpace(prologue))
            {
                chapters.Insert(0, new Chapter
                {
                    Index = 0,
                    Title = "序章",
                    Content = prologue,
                    CharOffset = 0
                });

                // Re-index all chapters
                for (var i = 0; i < chapters.Count; i++)
                    chapters[i].Index = i;
            }
        }

        return chapters;
    }
}
