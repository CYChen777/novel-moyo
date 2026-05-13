using System.IO;
using NovelMoyo.Models;
using NovelMoyo.Services.NovelParser;

namespace NovelMoyo.Services;

public class BookshelfService
{
    private readonly DataStore _dataStore;
    private readonly NovelParserFactory _parserFactory;
    private List<BookshelfEntry> _entries;
    private readonly Dictionary<string, Novel> _novelCache = [];

    public BookshelfService(DataStore dataStore, NovelParserFactory parserFactory)
    {
        _dataStore = dataStore;
        _parserFactory = parserFactory;
        _entries = _dataStore.LoadBookshelf();
    }

    public List<BookshelfEntry> GetAll() => _entries;

    public BookshelfEntry? GetEntry(string novelId)
    {
        return _entries.FirstOrDefault(e => e.NovelId == novelId);
    }

    public Novel? GetNovel(string novelId)
    {
        // Return cached novel if available
        if (_novelCache.TryGetValue(novelId, out var cached))
            return cached;

        var entry = GetEntry(novelId);
        if (entry is null || !File.Exists(entry.FilePath))
            return null;

        try
        {
            var parser = _parserFactory.GetParser(entry.FilePath);
            var novel = parser.Parse(entry.FilePath);
            novel.Id = entry.NovelId;
            novel.Title = entry.Title;
            novel.FilePath = entry.FilePath;
            novel.Format = entry.Format;
            novel.AddedAt = entry.AddedAt;
            novel.LastReadAt = entry.LastReadAt;
            _novelCache[novelId] = novel;
            return novel;
        }
        catch
        {
            return null;
        }
    }

    public void MarkAsRead(string novelId)
    {
        var entry = GetEntry(novelId);
        if (entry is not null)
        {
            entry.LastReadAt = DateTime.Now;
            _dataStore.SaveBookshelf(_entries);
        }
    }

    public BookshelfEntry AddNovel(Novel novel)
    {
        var existing = _entries.FirstOrDefault(e =>
            string.Equals(e.FilePath, novel.FilePath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.LastReadAt = DateTime.Now;
            novel.Id = existing.NovelId; // Sync novel ID to match bookshelf entry
            _novelCache.Remove(existing.NovelId);
            _dataStore.SaveBookshelf(_entries);
            return existing;
        }

        var entry = new BookshelfEntry
        {
            NovelId = novel.Id,
            Title = novel.Title,
            FilePath = novel.FilePath,
            Format = novel.Format,
            AddedAt = novel.AddedAt,
            LastReadAt = novel.LastReadAt
        };

        _entries.Add(entry);
        _dataStore.SaveBookshelf(_entries);
        return entry;
    }

    public bool RemoveNovel(string novelId)
    {
        var entry = GetEntry(novelId);
        if (entry is null)
            return false;

        _entries.Remove(entry);
        _novelCache.Remove(novelId);
        _dataStore.SaveBookshelf(_entries);
        _dataStore.DeleteProgress(novelId);
        return true;
    }
}
