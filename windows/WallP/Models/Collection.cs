namespace WallP.Models;

public sealed class WallPCollection
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required int WallhavenCollectionId { get; set; }
    public required string WallhavenUsername { get; set; }
    public DateTime? LastSynced { get; set; }
    public List<string> CachedImageIds { get; set; } = [];
}

public sealed class CachedImage
{
    public required string Id { get; init; }
    public required string WallhavenId { get; init; }
    public required string OriginalUrl { get; init; }
    public required string LocalFilename { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long FileSize { get; init; }
    public DateTime DateAdded { get; init; }
    public Guid CollectionId { get; init; }
}
