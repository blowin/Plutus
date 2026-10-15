using Plutus.Domain.Structure;

namespace Plutus.Infrastructure.Structure;

/// <summary>
/// The structural providers this project maintains. The dependency container discovers the same set by scanning for
/// <see cref="ISignatureSkeletonProvider"/> implementations, so a new provider registers itself by existing rather
/// than by editing a list here; the list is what a caller without a container (a test, a library host) uses.
/// <para>
/// Providers live in this layer because they depend on a parser: the domain describes what a skeleton is
/// (<see cref="ISignatureSkeletonProvider"/>, the token rewriter, the summaries) and never on Roslyn.
/// </para>
/// </summary>
public static class MaintainedSignatureProviders
{
    public static IReadOnlyList<ISignatureSkeletonProvider> All() => [new CSharpSignatureSkeletonProvider()];
}
