namespace Plutus.Domain.Minifier;

/// <summary>
/// The single entry point the bundle writers use. The string overload exists because a language reaches the writer
/// as the label <see cref="IMarkdownLanguageProvider"/> produced; everything inside the pipeline works with
/// <see cref="LanguageId"/>.
/// </summary>
public interface ICodeMinifier
{
    /// <summary>The processors this minifier can dispatch to.</summary>
    IReadOnlyCollection<ILanguageMinifier> SupportedMinifiers { get; }

    string Minify(string sourceCode, LanguageId language);

    string Minify(string sourceCode, string? language);
}
