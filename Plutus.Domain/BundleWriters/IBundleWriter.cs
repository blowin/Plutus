using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain.BundleWriters;

public interface IBundleWriter
{
    string FormatExtension { get; }

    public int MaxConcurrentFileReads { get; set; }

    ValueTask<List<BundleEntry>> WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        FileSize maxFileSize);
}
