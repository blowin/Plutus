using System.Collections.Concurrent;
using Plutus.Domain.Comments;
using Plutus.Domain.Structure;

namespace Plutus.Domain.Minifier;

/// <summary>Chooses the comment stripper for a language from the resolved plan and the maintained grammars.</summary>
public interface ICommentStripperResolver
{
    ICommentStripper Resolve(LanguageId language);
}

/// <summary>
/// The default resolver: ANTLR token channels only for a language the plan names and the inventory ships both a
/// grammar and a lexer for, lexical scanning for everything else. Resolved strippers are cached per language, so a
/// bundle of a thousand files does not build a thousand identical objects.
/// </summary>
public sealed class CommentStripperResolver(
    CommentStrippingPlan? plan = null,
    GrammarInventory? inventory = null,
    IReadOnlyDictionary<LanguageId, ICommentChannelLexer>? channelLexers = null,
    ICommentStripper? lexical = null) : ICommentStripperResolver
{
    private readonly ICommentStripper _lexical = lexical ?? new LexicalCommentStripper(new LanguageCommentSyntaxTable());
    private readonly ConcurrentDictionary<LanguageId, ICommentStripper> _cache = new();

    public ICommentStripper Resolve(LanguageId language) =>
        _cache.GetOrAdd(language, Create);

    private ICommentStripper Create(LanguageId language)
    {
        var grammarBacked = plan?.GrammarBackedLanguages ?? [];
        var isGrammarBacked = grammarBacked.Contains(language);

        if (isGrammarBacked
            && inventory is not null && inventory.TryGet(language, out var grammar)
            && channelLexers is not null && channelLexers.TryGetValue(language, out var lexer))
        {
            return new AntlrCommentChannelStripper(grammar, lexer);
        }

        return _lexical;
    }
}

/// <summary>
/// Everything one source-processing pass needs, resolved once per run and injected where it is used. Bundling it
/// keeps the language processors free of constructor parameters that are not theirs, and lets the container decide
/// what a run is configured to do.
/// </summary>
public sealed record SourceProcessingContext
{
    /// <summary>Processing nothing: no comments, no whitespace, no structural pass.</summary>
    public static readonly SourceProcessingContext Disabled = new()
    {
        Mode = SourceProcessingMode.None,
        CommentOptions = CommentStripOptions.Default,
        SignatureOptions = SignatureExtractionOptions.Default,
        ExtractSignatures = false,
        Strippers = new CommentStripperResolver(),
        Extractor = new SignatureSkeletonExtractor([]),
    };

    /// <summary>
    /// The default for a directly constructed processor: remove comments and collapse whitespace-only lines, the
    /// historical behavior of this class. The CLI always passes an explicit context, so a run is never described by
    /// this default.
    /// </summary>
    public static readonly SourceProcessingContext Default = new()
    {
        Mode = SourceProcessingMode.Minify,
        CommentOptions = CommentStripOptions.Default,
        SignatureOptions = SignatureExtractionOptions.Default,
        ExtractSignatures = false,
        Strippers = new CommentStripperResolver(),
        Extractor = new SignatureSkeletonExtractor([]),
    };

    public required SourceProcessingMode Mode { get; init; }

    public required CommentStripOptions CommentOptions { get; init; }

    public required SignatureExtractionOptions SignatureOptions { get; init; }

    /// <summary>True when a structural pass should run in addition to (or instead of) comment processing.</summary>
    public bool ExtractSignatures { get; init; }

    public required ICommentStripperResolver Strippers { get; init; }

    public required ISignatureSkeletonExtractor Extractor { get; init; }

    /// <summary>Receives the outcome of every comment pass, so the run can report what it removed.</summary>
    public ICommentStripObserver? CommentObserver { get; init; }

    /// <summary>True when this context would change at least one file.</summary>
    public bool IsActive => Mode != SourceProcessingMode.None || ExtractSignatures;
}
