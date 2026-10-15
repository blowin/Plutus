namespace Plutus.Domain.Minifier;

/// <summary>
/// A source processor for one or more languages. Each implementation declares the languages it answers for, so the
/// composition step maps languages to processors without a switch over identifiers and refuses to start when two
/// processors claim the same language.
/// </summary>
public interface ILanguageMinifier
{
    /// <summary>Every language this processor answers for. The first entry is its primary language.</summary>
    IReadOnlyCollection<LanguageId> Languages { get; }

    /// <summary>Description for display in the console (e.g., "C# Minifier (removes comments &amp; empty lines)").</summary>
    string Description { get; }

    /// <summary>Processes source code for the processor's primary language.</summary>
    string Minify(string sourceCode);

    /// <summary>Processes source code for one of the languages this processor declares.</summary>
    string Minify(string sourceCode, LanguageId language);
}
