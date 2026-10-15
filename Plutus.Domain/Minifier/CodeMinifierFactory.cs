using Plutus.Domain.Comments;
using Plutus.Domain.Structure;

namespace Plutus.Domain.Minifier;

/// <summary>
/// Turns the CLI flags into one <see cref="SourceProcessingContext"/> and then into the pipeline. The interaction
/// is fixed here, in one place:
/// <list type="bullet">
/// <item><description>neither <c>--strip-comments</c> nor <c>--minify</c> — comments and whitespace are untouched;</description></item>
/// <item><description><c>--strip-comments</c> — comments are removed and nothing else changes;</description></item>
/// <item><description><c>--minify</c> — comments are removed and whitespace-only lines are collapsed, so it is a
/// superset of <c>--strip-comments</c>;</description></item>
/// <item><description><c>--signatures-only</c> — a structural pass replaces implementation bodies, independently of
/// the two comment flags, and runs after comment removal and before whitespace collapsing;</description></item>
/// <item><description><c>--preserve-doc-comments</c> — applies to comment removal and to no other flag.</description></item>
/// </list>
/// </summary>
public static class CodeMinifierFactory
{
    /// <summary>Builds the context a run needs, from the resolved options and the services that report on it.</summary>
    public static SourceProcessingContext CreateContext(
        RunAppOptions options,
        CommentStrippingPlan? plan = null,
        GrammarInventory? inventory = null,
        IReadOnlyDictionary<LanguageId, ICommentChannelLexer>? channelLexers = null,
        ICommentStripObserver? commentObserver = null,
        IEnumerable<ISignatureSkeletonProvider>? signatureProviders = null,
        ISignatureExtractionObserver? signatureObserver = null)
    {
        var mode = options switch
        {
            { Minify: true } => SourceProcessingMode.Minify,
            { StripComments: true } => SourceProcessingMode.StripComments,
            _ => SourceProcessingMode.None,
        };

        var commentOptions = plan?.ToOptions(options.PreserveDocumentationComments)
                             ?? new CommentStripOptions
                             {
                                 Request = options.CommentStrategy,
                                 PreserveDocumentationComments = options.PreserveDocumentationComments,
                             };

        var providers = signatureProviders?.ToList() ?? SignatureSkeletonProviders.Maintained();

        return new SourceProcessingContext
        {
            Mode = mode,
            CommentOptions = commentOptions,
            SignatureOptions = options.SignatureOptions,
            ExtractSignatures = options.SignaturesOnly,
            Strippers = new CommentStripperResolver(plan, inventory, channelLexers),
            Extractor = new SignatureSkeletonExtractor(providers, signatureObserver),
            CommentObserver = commentObserver,
        };
    }

    /// <summary>Builds the minifier for a run, or the identity minifier when the run asked for nothing.</summary>
    public static ICodeMinifier Create(RunAppOptions options, SourceProcessingContext? context = null)
    {
        if (!options.StripComments && !options.Minify && !options.SignaturesOnly)
        {
            return IdentityCodeMinifier.Instance;
        }

        // --minify is a superset of --strip-comments, so when both are given the whitespace pass runs too; that is
        // decided by the context, which the caller can supply (the container does) or which is derived here.
        return CompositeCodeMinifier.CreateFull(context ?? CreateContext(options));
    }
}
