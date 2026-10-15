using System.Collections.Frozen;

namespace Plutus.Domain.Structure;

/// <summary>
/// The state of the structural pass at one moment: which provider answered, how many bodies of each kind were cut,
/// and how often the parser disagreed with the file. It is data, not text, so the host owns the wording.
/// </summary>
public sealed record SignatureExtractionSummary
{
    public long Files { get; init; }

    /// <summary>Bodies replaced with the terminator, by declaration kind.</summary>
    public IReadOnlyDictionary<MemberKind, long> BodiesByKind { get; init; } =
        new Dictionary<MemberKind, long>().ToFrozenDictionary();

    /// <summary>Number of files each provider answered for, keyed by provider name.</summary>
    public IReadOnlyDictionary<string, long> Providers { get; init; } = new Dictionary<string, long>().ToFrozenDictionary();

    /// <summary>Files whose language has no provider; they were passed through unchanged.</summary>
    public long FilesWithoutProvider { get; init; }

    public long FilesWithParseDiagnostics { get; init; }

    public long ParseDiagnostics { get; init; }

    /// <summary>Files a provider declined to rewrite, with the reason.</summary>
    public long Fallbacks { get; init; }

    public IReadOnlyCollection<string> FallbackReasons { get; init; } = [];

    public long StrippedBodies => BodiesByKind.Values.Sum();

    /// <summary>True once at least one file was processed structurally.</summary>
    public bool IsActive => Files > 0;
}
