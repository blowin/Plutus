using System.Collections.Frozen;

namespace Plutus.Domain.Structure;

/// <summary>
/// Produces a code skeleton for one language by walking a real parse tree and rewriting the token spans of
/// implementation bodies. This is the seam where a grammar-backed visitor plugs in — the Roslyn-backed C# provider
/// today, an ANTLR parse-tree visitor or a tree-sitter query tomorrow — and where the "AST, not regex" claim is
/// either earned or absent.
/// </summary>
public interface ISignatureSkeletonProvider
{
    /// <summary>The language this provider answers for.</summary>
    LanguageId Language { get; }

    /// <summary>Stable provider name used in messages and in the run report.</summary>
    string Name { get; }

    /// <summary>The declaration kinds this provider knows how to reduce to a signature.</summary>
    IReadOnlyCollection<MemberKind> SupportedMemberKinds { get; }

    /// <summary>
    /// Parses <paramref name="source"/> and returns its skeleton. Implementations are expected to be tolerant: a
    /// file that does not parse cleanly still yields a skeleton, with the diagnostic count reported.
    /// </summary>
    SignatureExtractionResult Create(string source, SignatureExtractionOptions options);
}

/// <summary>Receives the outcome of every structural pass, so a run can report what it actually did.</summary>
public interface ISignatureExtractionObserver
{
    void OnSignaturesExtracted(LanguageId language, SignatureExtractionResult result);
}

/// <summary>Dispatches structural extraction by language, and says so when no provider covers a language.</summary>
public interface ISignatureSkeletonExtractor
{
    IReadOnlyCollection<LanguageId> SupportedLanguages { get; }

    bool Supports(LanguageId language);

    SignatureExtractionResult Extract(string source, LanguageId language, SignatureExtractionOptions options);
}

/// <summary>The default extractor: a provider per language, with no guessing for the rest.</summary>
public sealed class SignatureSkeletonExtractor : ISignatureSkeletonExtractor
{
    private readonly FrozenDictionary<LanguageId, ISignatureSkeletonProvider> _providers;
    private readonly ISignatureExtractionObserver? _observer;

    public SignatureSkeletonExtractor(
        IEnumerable<ISignatureSkeletonProvider> providers,
        ISignatureExtractionObserver? observer = null)
    {
        var map = new Dictionary<LanguageId, ISignatureSkeletonProvider>();
        foreach (var provider in providers)
        {
            if (!map.TryAdd(provider.Language, provider))
            {
                throw new ArgumentException($"Two structural providers claim the language '{provider.Language}'.", nameof(providers));
            }
        }

        _providers = map.ToFrozenDictionary();
        _observer = observer;
    }

    public IReadOnlyCollection<LanguageId> SupportedLanguages => _providers.Keys;

    public bool Supports(LanguageId language) => _providers.ContainsKey(language);

    public SignatureExtractionResult Extract(string source, LanguageId language, SignatureExtractionOptions options)
    {
        var result = CreateResult(source, language, options);
        _observer?.OnSignaturesExtracted(language, result);
        return result;
    }

    private SignatureExtractionResult CreateResult(string source, LanguageId language, SignatureExtractionOptions options)
    {
        if (!_providers.TryGetValue(language, out var provider))
        {
            return Unsupported(source, language);
        }

        return string.IsNullOrEmpty(source)
            ? new SignatureExtractionResult { Content = source, Language = language, Provider = provider.Name }
            : provider.Create(source, options);
    }

    private static SignatureExtractionResult Unsupported(string source, LanguageId language) => new()
    {
        Content = source,
        Language = language,
        LanguageSupported = false,
        FallbackReason = $"no structural provider is maintained for '{language}'",
    };
}
