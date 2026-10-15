using System.Collections.Frozen;

namespace Plutus.Domain.Structure;

/// <summary>
/// What one structural pass did to one file: the skeleton, which kinds of member body were cut, and what the parser
/// thought of the input. All of it is reported, so a skeleton is never presented as a full parse when it is not.
/// </summary>
public sealed record SignatureExtractionResult
{
    private static readonly IReadOnlyDictionary<MemberKind, int> NoBodies = new Dictionary<MemberKind, int>().ToFrozenDictionary();

    /// <summary>The skeleton, or the input unchanged when no provider applies.</summary>
    public required string Content { get; init; }

    public required LanguageId Language { get; init; }

    /// <summary>The provider that answered, for example <c>roslyn-csharp</c>.</summary>
    public string? Provider { get; init; }

    /// <summary>
    /// <c>false</c> when no structural provider is maintained for the language: the file is passed through untouched
    /// rather than approximated with a text heuristic.
    /// </summary>
    public bool LanguageSupported { get; init; } = true;

    /// <summary>How many bodies of each kind were replaced with the structural terminator.</summary>
    public IReadOnlyDictionary<MemberKind, int> StrippedBodies { get; init; } = NoBodies;

    /// <summary>Diagnostics the parser produced. A non-zero value means the file did not parse cleanly.</summary>
    public int ParseDiagnostics { get; init; }

    /// <summary>Set when a provider declined to rewrite the file, with the reason it declined.</summary>
    public string? FallbackReason { get; init; }

    public int StrippedBodyCount => StrippedBodies.Values.Sum();

    public int CountOf(MemberKind kind) => StrippedBodies.TryGetValue(kind, out var count) ? count : 0;
}
