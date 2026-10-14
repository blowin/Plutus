namespace Plutus.Domain.Diffing;

/// <summary>
/// Selects the revision range used to isolate bundle context to changed files.
/// </summary>
public sealed record GitDiffOptions
{
    /// <summary>Base revision to compare against. Equivalent to <c>HEAD</c> when <see cref="Target"/> is not set.</summary>
    public string? Target { get; init; }
}
