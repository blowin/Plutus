using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier;

/// <summary>
/// Turns the CLI flags into the source-processing pipeline. The interaction is fixed here, in one place:
/// <list type="bullet">
/// <item><description>neither <c>--strip-comments</c> nor <c>--minify</c> — nothing is touched;</description></item>
/// <item><description><c>--strip-comments</c> — comments are removed and nothing else changes;</description></item>
/// <item><description><c>--minify</c> — comments are removed and whitespace-only lines are collapsed, so it is a
/// superset of <c>--strip-comments</c>;</description></item>
/// <item><description><c>--preserve-doc-comments</c> — applies to both of the above and to no other flag.</description></item>
/// </list>
/// </summary>
public static class CodeMinifierFactory
{
    public static ICodeMinifier Create(
        RunAppOptions options,
        ICommentStripObserver? observer = null,
        CommentStrippingPlan? plan = null,
        GrammarInventory? inventory = null,
        IReadOnlyDictionary<string, ICommentChannelLexer>? channelLexers = null)
    {
        if (!options.StripComments && !options.Minify)
        {
            return IdentityCodeMinifier.Instance;
        }

        var stripOptions = plan?.ToOptions(options.PreserveDocumentationComments)
                           ?? new CommentStripOptions
                           {
                               Request = options.CommentStrategy,
                               PreserveDocumentationComments = options.PreserveDocumentationComments,
                           };

        // --minify is a superset of --strip-comments, so when both are given the whitespace pass runs too.
        var mode = options.Minify ? SourceProcessingMode.Minify : SourceProcessingMode.StripComments;

        return CompositeCodeMinifier.CreateFull(stripOptions, mode, observer, plan, inventory, channelLexers);
    }
}
