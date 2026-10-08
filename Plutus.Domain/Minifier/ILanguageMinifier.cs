namespace Plutus.Domain.Minifier;

public interface ILanguageMinifier
{
    /// <summary>
    /// The language name corresponding to Markdown formatting (e.g., "csharp", "javascript").
    /// </summary>
    string Language { get; }

    /// <summary>
    /// Description for display in the console (e.g., "C# Minifier (removes comments & empty lines)").
    /// </summary>
    string Description { get; }

    string Minify(string sourceCode);
}
