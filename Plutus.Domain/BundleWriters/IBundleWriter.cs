using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diffing;
using Plutus.Domain.Tokens;

namespace Plutus.Domain.BundleWriters;

public interface IBundleWriter
{
    string FormatExtension { get; }

    public int MaxConcurrentFileReads { get; set; }

    /// <summary>When <c>false</c>, file contents are read for status only and never embedded in the bundle.</summary>
    bool IncludeFileContents { get; set; }

    ValueTask<List<BundleEntry>> WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        FileSize maxFileSize,
        GitDiffContext? gitDiff = null,
        TokenBudgetSummary? diffBudgetSummary = null);
}
