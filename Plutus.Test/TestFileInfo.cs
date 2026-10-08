using Microsoft.Extensions.FileProviders;

namespace Plutus.Test;

public sealed class TestFileInfo : IFileInfo
{
    public MemoryStream Stream { get; set; } = new();
    public MemoryStream GetResetStream() { Stream.Position = 0; return Stream; }

    public Stream CreateReadStream() => new MemoryStream(Stream.ToArray());
    public bool Exists => true;
    public long Length => Stream.Length;
    public string? PhysicalPath { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset LastModified => new DateTimeOffset(2026, 10, 7, 17, 58, 12, TimeSpan.Zero);
    public bool IsDirectory => false;
}
