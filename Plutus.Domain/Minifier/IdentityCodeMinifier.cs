namespace Plutus.Domain.Minifier;

public sealed class IdentityCodeMinifier : ICodeMinifier
{
    public static readonly ICodeMinifier Instance = new IdentityCodeMinifier();

    public IReadOnlyCollection<ILanguageMinifier> SupportedMinifiers => Array.Empty<ILanguageMinifier>();

    public string Minify(string sourceCode, string language) => sourceCode;
}
