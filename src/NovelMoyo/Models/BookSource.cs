using System.Collections.Generic;

namespace NovelMoyo.Models;

public class BookSourceConfig
{
    public List<BookSource> BookSources { get; set; } = [];
}

public class BookSource
{
    public string Title { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = string.Empty;
    public string Charset { get; set; } = "utf-8";
    public SearchConfig Search { get; set; } = new();
    public CatalogConfig Catalog { get; set; } = new();
    public ContentConfig Content { get; set; } = new();
    public PaginationConfig? Pagination { get; set; }
}

public class SearchConfig
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Params { get; set; } = string.Empty;
    public string ResultType { get; set; } = "html"; // "html" or "json"
    public SelectorConfig? Selectors { get; set; }
    public JsonPathConfig? JsonPath { get; set; }
}

public class CatalogConfig
{
    public string ResultType { get; set; } = "html";
    public string? UrlTemplate { get; set; }
    public SelectorConfig? Selectors { get; set; }
    public JsonPathConfig? JsonPath { get; set; }
}

public class ContentConfig
{
    public string ResultType { get; set; } = "html";
    public string? UrlTemplate { get; set; }
    public SelectorConfig? Selectors { get; set; }
    public JsonPathConfig? JsonPath { get; set; }
    public ContentFilter? Filter { get; set; }
}

public class PaginationConfig
{
    public string? CatalogNext { get; set; }
    public string? ContentNext { get; set; }
}

public class SelectorConfig
{
    public string Item { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Url { get; set; }
    public string? LatestChapter { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
}

public class JsonPathConfig
{
    public string Item { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Url { get; set; }
    public string? LatestChapter { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
}

public class ContentFilter
{
    public string Type { get; set; } = "remove_keywords";
    public List<string> Keywords { get; set; } = [];
}
