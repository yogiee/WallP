using System.Text.Json.Serialization;

namespace WallP.Models;

public sealed class WallhavenSearchResponse
{
    [JsonPropertyName("data")] public List<WallhavenWallpaper> Data { get; init; } = [];
    [JsonPropertyName("meta")] public WallhavenMeta Meta { get; init; } = new();
}

public sealed class WallhavenWallpaper
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("url")] public string Url { get; init; } = "";
    [JsonPropertyName("short_url")] public string? ShortUrl { get; init; }
    [JsonPropertyName("views")] public int Views { get; init; }
    [JsonPropertyName("favorites")] public int Favorites { get; init; }
    [JsonPropertyName("source")] public string Source { get; init; } = "";
    [JsonPropertyName("purity")] public string Purity { get; init; } = "";
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    [JsonPropertyName("dimension_x")] public int DimensionX { get; init; }
    [JsonPropertyName("dimension_y")] public int DimensionY { get; init; }
    [JsonPropertyName("resolution")] public string Resolution { get; init; } = "";
    [JsonPropertyName("ratio")] public string Ratio { get; init; } = "";
    [JsonPropertyName("file_size")] public long FileSize { get; init; }
    [JsonPropertyName("file_type")] public string FileType { get; init; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; init; } = "";
    [JsonPropertyName("colors")] public List<string> Colors { get; init; } = [];
    [JsonPropertyName("path")] public string Path { get; init; } = "";
    [JsonPropertyName("thumbs")] public WallhavenThumbs Thumbs { get; init; } = new();
}

public sealed class WallhavenThumbs
{
    [JsonPropertyName("large")] public string Large { get; init; } = "";
    [JsonPropertyName("original")] public string Original { get; init; } = "";
    [JsonPropertyName("small")] public string Small { get; init; } = "";
}

public sealed class WallhavenMeta
{
    [JsonPropertyName("current_page")] public int CurrentPage { get; init; }
    [JsonPropertyName("last_page")] public int LastPage { get; init; }
    [JsonPropertyName("per_page")] public int PerPage { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
    [JsonPropertyName("query")] public string? Query { get; init; }
    [JsonPropertyName("seed")] public string? Seed { get; init; }
}

public sealed class WallhavenCollectionsResponse
{
    [JsonPropertyName("data")] public List<WallhavenCollection> Data { get; init; } = [];
}

public sealed class WallhavenCollection
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("label")] public string Label { get; init; } = "";
    [JsonPropertyName("views")] public int Views { get; init; }
    [JsonPropertyName("public")] public int Public { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
}
