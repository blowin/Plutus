using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// Base class for every language processor. It applies the three passes in a fixed order — comments, structural
/// signatures, whitespace — and delegates each to a service that knows the language, so an implementation only
/// declares the languages it serves plus any behavior that is genuinely its own.
/// </summary>
public abstract class BaseLanguageMinifier(SourceProcessingContext? context = null) : ILanguageMinifier
{
    // Matches any line containing only optional whitespace followed by a newline character sequence.
    // The (?m) flag enables multi-line mode so ^ anchors to the start of individual lines.
    private static readonly Regex BlankLineStripPattern = new(@"(?m)^[ \t]*\r?\n", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    public abstract IReadOnlyCollection<LanguageId> Languages { get; }

    public abstract string Description { get; }

    /// <summary>The first declared language, used by the single-argument overloads.</summary>
    public LanguageId PrimaryLanguage => Languages.First();

    protected SourceProcessingContext Context { get; } = context ?? SourceProcessingContext.Default;

    public string Minify(string sourceCode) => Minify(sourceCode, PrimaryLanguage);

    public string Minify(string sourceCode, LanguageId language)
    {
        if (string.IsNullOrEmpty(sourceCode))
        {
            return sourceCode;
        }

        try
        {
            var result = sourceCode;

            // 1. Comments. A language with no syntax entry is passed through untouched rather than scanned with
            //    another language's rules, and that ends the pass for the file.
            if (Context.Mode != SourceProcessingMode.None)
            {
                var stripped = Context.Strippers.Resolve(language).Strip(sourceCode, language, Context.CommentOptions);
                Context.CommentObserver?.OnCommentsStripped(language, stripped);

                if (!stripped.LanguageSupported)
                {
                    return sourceCode;
                }

                result = stripped.Content;
            }

            // 2. Hooks that belong to specific languages and are not comment removal.
            result = AfterCommentStrip(result);

            // 3. Structural signatures: a parse-tree pass that replaces implementation bodies. A language without a
            //    provider comes back unchanged, with the reason recorded by the extractor.
            if (Context.ExtractSignatures)
            {
                result = Context.Extractor.Extract(result, language, Context.SignatureOptions).Content;
            }

            if (Context.Mode != SourceProcessingMode.Minify)
            {
                return result;
            }

            // 4. Minification proper: language boilerplate, then whitespace-only lines, then the outer trim.
            result = MinifyWhitespace(result);
            result = BlankLineStripPattern.Replace(result, string.Empty);
            return result.Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            return sourceCode;
        }
    }

    /// <summary>Optional hook that runs in every mode, after comments are removed.</summary>
    protected virtual string AfterCommentStrip(string sourceCode) => sourceCode;

    /// <summary>Optional hook that runs only in <see cref="SourceProcessingMode.Minify"/>.</summary>
    protected virtual string MinifyWhitespace(string sourceCode) => sourceCode;
}
