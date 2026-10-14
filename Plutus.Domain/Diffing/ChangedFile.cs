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
/// A deleted file is reported under the revision being compared against, so its path still exists there
/// even though the working tree no longer has it.
/// </summary>
public sealed record ChangedFile(string Path, ChangeKind Kind);
