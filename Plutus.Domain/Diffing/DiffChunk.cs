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
/// The diff of a single file, kept as its own unit so size and token limits can exclude one large change
/// without discarding the rest of the bundle. <see cref="Text"/> is the file's verbatim <c>git diff</c>
/// block, including its <c>diff --git</c> header.
/// </summary>
public sealed record DiffChunk(
    string RepositoryPath,
    string Path,
    ChangeKind Kind,
    string Text,
    int AddedLines,
    int DeletedLines)
{
    /// <summary>Token estimate of the chunk text, computed when size limits are applied.</summary>
    public int EstimatedTokens { get; init; }

    /// <summary>Set when the chunk was left out, either because it was too large or by the token budget.</summary>
    public string? SkipReason { get; init; }

    public bool IsSkipped => SkipReason is not null;

    public int LineCount => AddedLines + DeletedLines;
}
