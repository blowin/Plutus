using Plutus.Domain.Comments;
using Plutus.Domain.Structure;

namespace Plutus.Domain.Minifier;

/// <summary>
/// Turns the resolved options into one <see cref="SourceProcessingContext"/> and then into the pipeline. The
/// interaction is fixed here, in one place:
/// <list type="bullet">
/// <item><description>neither <c>--strip-comments</c> nor <c>--minify</c> — comments and whitespace are untouched;</description></item>
/// <item><description><c>--strip-comments</c> — comments are removed and nothing else changes;</description></item>
/// <item><description><c>--minify</c> — comments are removed and whitespace-only lines are collapsed, so it is a
/// superset of <c>--strip-comments</c>;</description></item>
/// <item><description><c>--signatures-only</c> — a structural pass replaces implementation bodies, independently of
/// the two comment flags, and runs after comment removal and before whitespace collapsing;</description></item>
/// <item><description><c>--preserve-doc-comments</c> — applies to comment removal and to no other flag.</description></item>
/// </list>
/// <para>
/// The factory is a service: the options and the collaborating services arrive through the constructor, so building
/// the pipeline of a run is one call and a test supplies exactly the pieces it is about.
/// </para>
/// </summary>
public sealed class CodeMinifierFactory
{
    private readonly RunAppOptions _options;
    private readonly ILanguageCommentSyntaxTable _syntax;
    private readonly GrammarInventory? _inventory;
    private readonly IReadOnlyDictionary<LanguageId, ICommentChannelLexer>? _channelLexers;
    private readonly IEnumerable<ISignatureSkeletonProvider>? _signatureProviders;
    private readonly ICommentStripObserver? _commentObserver;
    private readonly ISignatureExtractionObserver? _signatureObserver;
    private readonly Lazy<SourceProcessingContext> _context;

    public CodeMinifierFactory(
        RunAppOptions options,
        ILanguageCommentSyntaxTable syntax,
        GrammarInventory? inventory = null,
        IReadOnlyDictionary<LanguageId, ICommentChannelLexer>? channelLexers = null,
        IEnumerable<ISignatureSkeletonProvider>? signatureProviders = null,
        ICommentStripObserver? commentObserver = null,
        ISignatureExtractionObserver? signatureObserver = null)
    {
        _options = options;
        _syntax = syntax;
        _inventory = inventory;
        _channelLexers = channelLexers;
        _signatureProviders = signatureProviders;
        _commentObserver = commentObserver;
        _signatureObserver = signatureObserver;
        _context = new Lazy<SourceProcessingContext>(CreateContext);
    }

    /// <summary>
    /// The one context of this run. The container and the pipeline share it, so a resolved stripper is cached once
    /// instead of twice and every pass reports to the same observers.
    /// </summary>
    public SourceProcessingContext Context => _context.Value;

    /// <summary>Builds the context a run needs, from the resolved options and the services that report on it.</summary>
    public SourceProcessingContext CreateContext()
    {
        var mode = _options switch
        {
            { Minify: true } => SourceProcessingMode.Minify,
            { StripComments: true } => SourceProcessingMode.StripComments,
            _ => SourceProcessingMode.None,
        };

        var plan = _options.CommentStripPlan;
        var commentOptions = plan?.ToOptions(_options.PreserveDocumentationComments)
                             ?? new CommentStripOptions
                             {
                                 Request = _options.CommentStrategy,
                                 PreserveDocumentationComments = _options.PreserveDocumentationComments,
                             };

        // Providers are supplied by the host: the domain describes what a skeleton is and never depends on a
        // parser, so a context built without providers simply has no structural support.
        var providers = _signatureProviders?.ToList() ?? [];

        return new SourceProcessingContext
        {
            Mode = mode,
            CommentOptions = commentOptions,
            SignatureOptions = _options.SignatureOptions,
            ExtractSignatures = _options.SignaturesOnly,
            Strippers = new CommentStripperResolver(
                plan,
                _inventory,
                _channelLexers,
                new LexicalCommentStripper(_syntax)),
            Extractor = new SignatureSkeletonExtractor(providers, _signatureObserver),
            CommentObserver = _commentObserver,
        };
    }

    /// <summary>Builds the minifier for a run, or the identity minifier when the run asked for nothing.</summary>
    public ICodeMinifier Create()
    {
        if (!_options.StripComments && !_options.Minify && !_options.SignaturesOnly)
        {
            return new IdentityCodeMinifier();
        }

        // --minify is a superset of --strip-comments, so when both are given the whitespace pass runs too; which
        // passes are active is decided by the context.
        return new CompositeCodeMinifier(new LanguageMinifierCatalog(_syntax).Create(Context));
    }
}
