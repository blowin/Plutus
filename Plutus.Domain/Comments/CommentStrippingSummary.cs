using System.Collections.Frozen;

namespace Plutus.Domain.Comments;

/// <summary>
/// The state of comment processing at one moment: which strategy answered, what it removed, and what it could not
/// do. It is data, not text — the host decides how to print it — which is what keeps a run's wording in one place.
/// </summary>
public sealed record CommentStrippingSummary
{
    /// <summary>What <c>--comment-strategy</c> asked for.</summary>
    public required CommentStrategyRequest Request { get; init; }

    /// <summary>The strategy that answered for languages without a maintained grammar.</summary>
    public required CommentStripStrategy Strategy { get; init; }

    /// <summary>Why that strategy was chosen.</summary>
    public required string Rationale { get; init; }

    public long Files { get; init; }

    public long RemovedInline { get; init; }

    public long RemovedBlock { get; init; }

    public long RemovedDocumentation { get; init; }

    public long PreservedDocumentation { get; init; }

    public long Unterminated { get; init; }

    public long Fallbacks { get; init; }

    /// <summary>Files per language that actually went through a stripper.</summary>
    public IReadOnlyDictionary<LanguageId, long> Languages { get; init; } = new Dictionary<LanguageId, long>().ToFrozenDictionary();

    /// <summary>The strategy that answered for each language.</summary>
    public IReadOnlyDictionary<LanguageId, CommentStripStrategy> Strategies { get; init; } =
        new Dictionary<LanguageId, CommentStripStrategy>().ToFrozenDictionary();

    /// <summary>Files whose stripper declined to transform them, with the reason.</summary>
    public IReadOnlyCollection<string> FallbackReasons { get; init; } = [];

    public long RemovedComments => RemovedInline + RemovedBlock + RemovedDocumentation;

    /// <summary>True once at least one file went through a stripper.</summary>
    public bool IsActive => Files > 0;
}
