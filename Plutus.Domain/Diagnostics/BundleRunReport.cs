namespace Plutus.Domain.Diagnostics;

/// <summary>
/// What one bundle run produced: the numbers the host needs to print a summary. The bundler records them and the
/// host renders them, so the numbers and their wording are not entangled.
/// </summary>
public sealed class BundleRunReport
{
    /// <summary>The path of the written bundle, or <c>null</c> when the run did not get that far.</summary>
    public string? OutputPath { get; private set; }

    public int FilesIncluded { get; private set; }

    public long TotalBytes { get; private set; }

    public TimeSpan Elapsed { get; private set; }

    /// <summary>True once a bundle was actually written.</summary>
    public bool HasResult => OutputPath is not null;

    public void Record(string outputPath, int filesIncluded, long totalBytes, TimeSpan elapsed)
    {
        OutputPath = outputPath;
        FilesIncluded = filesIncluded;
        TotalBytes = totalBytes;
        Elapsed = elapsed;
    }
}
