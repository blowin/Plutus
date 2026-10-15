using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Minifier;
using Plutus.Domain.Structure;
using Plutus.Infrastructure.Structure;

namespace Plutus.Test;

/// <summary>
/// Builds the source-processing pipeline the way the container does, so a test exercises the same composition:
/// the maintained language processors plus the structural providers this build ships. Providers live in the
/// infrastructure layer, and a context built without them has no structural support at all — which is exactly what a
/// test would otherwise trip over.
/// </summary>
internal static class TestSourceProcessing
{
    /// <summary>The comment syntax table of this build.</summary>
    public static ILanguageCommentSyntaxTable Syntax { get; } = new LanguageCommentSyntaxTable();

    /// <summary>The structural providers this build ships, discovered the way the container discovers them.</summary>
    public static IReadOnlyList<ISignatureSkeletonProvider> Providers() =>
        typeof(CSharpSignatureSkeletonProvider).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(ISignatureSkeletonProvider).IsAssignableFrom(type))
            .Select(type => (ISignatureSkeletonProvider)Activator.CreateInstance(type)!)
            .ToList();

    /// <summary>The pipeline a run with these options gets, with the maintained processors and providers.</summary>
    public static ICodeMinifier Create(
        RunAppOptions options,
        ICommentStripObserver? commentObserver = null,
        ISignatureExtractionObserver? signatureObserver = null) =>
        Factory(options, commentObserver, signatureObserver).Create();

    /// <summary>The factory itself, for tests that assert on the context a run is built with.</summary>
    public static CodeMinifierFactory Factory(
        RunAppOptions options,
        ICommentStripObserver? commentObserver = null,
        ISignatureExtractionObserver? signatureObserver = null) =>
        new(options, Syntax, signatureProviders: Providers(), commentObserver: commentObserver, signatureObserver: signatureObserver);

    /// <summary>The processors a run gets: maintained ones plus a generic one for every other supported language.</summary>
    public static IReadOnlyList<ILanguageMinifier> Minifiers(SourceProcessingContext context) =>
        Catalog().Compose(Catalog().Maintained(context), context);

    /// <summary>The minifier a run gets for an explicitly built context.</summary>
    public static ICodeMinifier Minifier(SourceProcessingContext context) =>
        new CompositeCodeMinifier(Minifiers(context));

    private static LanguageMinifierCatalog Catalog() => new(Syntax);
}
