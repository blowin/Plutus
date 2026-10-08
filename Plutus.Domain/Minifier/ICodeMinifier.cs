namespace Plutus.Domain.Minifier;

public interface ICodeMinifier
{
    /// <summary>
    /// List of currently supported languages.
    /// </summary>
    IReadOnlyCollection<ILanguageMinifier> SupportedMinifiers { get; }

    string Minify(string sourceCode, string language);
}
