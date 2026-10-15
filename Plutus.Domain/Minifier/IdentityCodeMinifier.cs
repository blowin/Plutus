namespace Plutus.Domain.Minifier;

/// <summary>The minifier used when a run asks for no source processing at all.</summary>
public sealed class IdentityCodeMinifier : ICodeMinifier
{
    public IReadOnlyCollection<ILanguageMinifier> SupportedMinifiers => [];

    public string Minify(string sourceCode, LanguageId language) => sourceCode;

    public string Minify(string sourceCode, string? language) => sourceCode;
}
