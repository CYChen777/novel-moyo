using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NovelMoyo.Models;

namespace NovelMoyo.Services.BookSource;

public class BookSourceService
{
    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StealthReader");

    private static readonly string SourcesPath = Path.Combine(AppDataRoot, "sources.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private BookSourceConfig _config = new();

    public BookSourceService()
    {
        Load();
    }

    public List<Models.BookSource> GetAll() => _config.BookSources;

    public List<Models.BookSource> GetEnabled() => _config.BookSources.Where(s => s.Enabled).ToList();

    public void Load()
    {
        if (!File.Exists(SourcesPath))
        {
            _config = CreateDefaultConfig();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(SourcesPath);
            _config = JsonSerializer.Deserialize<BookSourceConfig>(json, JsonOptions) ?? new BookSourceConfig();
        }
        catch
        {
            _config = CreateDefaultConfig();
            Save();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataRoot);
        var json = JsonSerializer.Serialize(_config, JsonOptions);
        File.WriteAllText(SourcesPath, json);
    }

    public void Add(Models.BookSource source)
    {
        _config.BookSources.Add(source);
        Save();
    }

    public void Remove(string title)
    {
        _config.BookSources.RemoveAll(s => s.Title == title);
        Save();
    }

    public void Update(Models.BookSource source)
    {
        var index = _config.BookSources.FindIndex(s => s.Title == source.Title);
        if (index >= 0)
        {
            _config.BookSources[index] = source;
            Save();
        }
    }

    private static Models.BookSource CreateBqgSource(string title, string host)
    {
        return new Models.BookSource
        {
            Title = title,
            Enabled = true,
            Host = host,
            Charset = "utf-8",
            Search = new SearchConfig
            {
                Url = $"{host}/api/search?q={{keyword}}",
                Method = "GET",
                ResultType = "json",
                JsonPath = new JsonPathConfig
                {
                    Item = "$.data[*]",
                    Name = "$.title",
                    Author = "$.author",
                    Url = "$.id",
                    LatestChapter = "$.intro"
                }
            },
            Catalog = new CatalogConfig
            {
                ResultType = "json",
                UrlTemplate = $"{host}/api/booklist?id={{bookId}}",
                JsonPath = new JsonPathConfig
                {
                    Item = "$.list[*]",
                    Title = "$"
                }
            },
            Content = new ContentConfig
            {
                ResultType = "json",
                UrlTemplate = $"{host}/api/chapter?id={{bookId}}&chapterid={{chapterId}}",
                JsonPath = new JsonPathConfig
                {
                    Title = "$.chaptername",
                    Body = "$.txt"
                }
            }
        };
    }

    private static BookSourceConfig CreateDefaultConfig()
    {
        return new BookSourceConfig
        {
            BookSources =
            [
                CreateBqgSource("笔趣阁API站1", "https://m.bqg355.xyz"),
                CreateBqgSource("笔趣阁API站2", "https://www.bqg355.xyz"),
                CreateBqgSource("笔趣阁API站3", "https://bqg355.xyz")
            ]
        };
    }
}
