using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Minifier;
using Plutus.Domain.Structure;
using Plutus.Infrastructure.Structure;

namespace Plutus.Test;

/// <summary>
/// Builds the source-processing pipeline the way the container does, so a test exercises the same composition:
/// maintained language processors plus the structural providers this build ships. Providers live in the
/// infrastructure layer, and a context built without them has no structural support at all — which is exactly what
/// a test would otherwise trip over.
/// </summary>
internal static class TestSourceProcessing
{
    public static ICodeMinifier Create(
        RunAppOptions options,
        ICommentStripObserver? commentObserver = null,
        ISignatureExtractionObserver? signatureObserver = null) =>
        CodeMinifierFactory.Create(
            options,
            CodeMinifierFactory.CreateContext(
                options,
                options.CommentStripPlan,
                inventory: null,
                channelLexers: null,
                commentObserver,
                MaintainedSignatureProviders.All(),
                signatureObserver));
}
