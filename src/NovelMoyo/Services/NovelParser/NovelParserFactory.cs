using System.IO;

namespace NovelMoyo.Services.NovelParser;

public class NovelParserFactory
{
    private readonly TxtParser _txtParser = new();
    private readonly EpubParser _epubParser = new();

    public INovelParser GetParser(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        INovelParser parser = extension switch
        {
            ".txt" => _txtParser,
            ".epub" => _epubParser,
            _ => throw new NotSupportedException($"Unsupported file format: {extension}")
        };
        return parser;
    }
}
