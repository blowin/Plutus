namespace Plutus.Domain.Diffing;

/// <summary>Kind of change a file underwent between two revisions.</summary>
public enum ChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    TypeChanged
}

/// <summary>
/// A single path reported by a git diff, expressed with forward slashes and relative to a bundle root.
/// Deleted files are reported with the revision being compared against, so every path still exists on disk.
/// </summary>
public sealed record ChangedFile(string Path, ChangeKind Kind);
