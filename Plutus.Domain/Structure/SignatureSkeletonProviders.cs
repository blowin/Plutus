namespace Plutus.Domain.Structure;

/// <summary>
/// The structural providers this project maintains. The dependency container discovers the same set by scanning for
/// <see cref="ISignatureSkeletonProvider"/> implementations, so a new provider is registered by existing rather than
/// by editing a list here.
/// </summary>
public static class SignatureSkeletonProviders
{
    public static IReadOnlyList<ISignatureSkeletonProvider> Maintained() => [new CSharpSignatureSkeletonProvider()];
}
