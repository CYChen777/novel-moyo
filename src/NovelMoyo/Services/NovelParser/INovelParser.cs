using NovelMoyo.Models;

namespace NovelMoyo.Services.NovelParser;

public interface INovelParser
{
    Novel Parse(string filePath);
}
