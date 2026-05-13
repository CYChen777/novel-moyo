using System.IO;
using System.Linq;
using System.Text.Json;
using NovelMoyo.Models;

namespace NovelMoyo.Services;

public class DataStore
{
    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StealthReader");

    private static readonly string SettingsPath = Path.Combine(AppDataRoot, "settings.json");
    private static readonly string BookshelfPath = Path.Combine(AppDataRoot, "bookshelf.json");
    private static readonly string ProgressDir = Path.Combine(AppDataRoot, "progress");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public DataStore()
    {
        EnsureDirectories();
    }

    private static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(ProgressDir);
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            // Merge in any default hotkeys that are missing from the saved file
            // (e.g. new hotkeys added in a newer version)
            var defaults = new AppSettings();
            foreach (var (action, combo) in defaults.Hotkeys)
            {
                if (!settings.Hotkeys.ContainsKey(action))
                    settings.Hotkeys[action] = combo;
            }
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        WriteJsonAtomically(SettingsPath, settings);
    }

    public List<BookshelfEntry> LoadBookshelf()
    {
        if (!File.Exists(BookshelfPath))
            return [];

        try
        {
            var json = File.ReadAllText(BookshelfPath);
            return JsonSerializer.Deserialize<List<BookshelfEntry>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SaveBookshelf(List<BookshelfEntry> entries)
    {
        WriteJsonAtomically(BookshelfPath, entries);
    }

    public ReadingProgress? LoadProgress(string novelId)
    {
        var path = GetProgressPath(novelId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ReadingProgress>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void SaveProgress(ReadingProgress progress)
    {
        var path = GetProgressPath(progress.NovelId);
        WriteJsonAtomically(path, progress);
    }

    public void DeleteProgress(string novelId)
    {
        var path = GetProgressPath(novelId);
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch { /* best effort */ }
        }
    }

    private static string GetProgressPath(string novelId)
    {
        // Sanitize novelId for use as a filename — replace any invalid chars
        var safeId = string.Concat(novelId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(ProgressDir, $"{safeId}.json");
    }

    private static void WriteJsonAtomically<T>(string targetPath, T data)
    {
        var tempPath = targetPath + ".tmp";
        var backupPath = targetPath + ".bak";
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(tempPath, json);
            if (File.Exists(targetPath))
            {
                // File.Replace is atomic on NTFS and creates a backup of the original
                File.Replace(tempPath, targetPath, backupPath);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best effort */ }
            throw;
        }
    }
}
