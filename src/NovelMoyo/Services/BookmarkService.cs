using NovelMoyo.Models;

namespace NovelMoyo.Services;

public class BookmarkService
{
    private readonly DataStore _dataStore;

    public BookmarkService(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public List<Bookmark> GetBookmarks(string novelId)
    {
        var progress = _dataStore.LoadProgress(novelId);
        return progress?.Bookmarks ?? [];
    }

    public void AddBookmark(string novelId, Bookmark bookmark)
    {
        var progress = _dataStore.LoadProgress(novelId) ?? new ReadingProgress { NovelId = novelId };
        progress.Bookmarks.Add(bookmark);
        progress.UpdatedAt = DateTime.Now;
        _dataStore.SaveProgress(progress);
    }

    public bool RemoveBookmark(string novelId, string bookmarkId)
    {
        var progress = _dataStore.LoadProgress(novelId);
        if (progress is null) return false;

        var bookmark = progress.Bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
        if (bookmark is null) return false;

        progress.Bookmarks.Remove(bookmark);
        progress.UpdatedAt = DateTime.Now;
        _dataStore.SaveProgress(progress);
        return true;
    }

    public void SaveProgressWithBookmarks(ReadingProgress progress)
    {
        _dataStore.SaveProgress(progress);
    }

    public ReadingProgress? LoadProgress(string novelId)
    {
        return _dataStore.LoadProgress(novelId);
    }
}
