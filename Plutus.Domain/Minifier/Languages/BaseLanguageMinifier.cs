using System.Text.RegularExpressions;
using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// Base class for every language minifier. Comment removal is delegated to an <see cref="ICommentStripper"/>,
/// which is the lexical scanner unless a project-maintained ANTLR grammar was selected for that language.
/// </summary>
public abstract class BaseLanguageMinifier(
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null) : ILanguageMinifier
{
    // Matches any line containing only optional whitespace followed by a newline character sequence.
    // The (?m) flag enables multi-line mode so ^ anchors to the start of individual lines.
    private static readonly Regex BlankLineStripPattern = new(@"(?m)^[ \t]*\r?\n", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    public abstract string Language { get; }

    public abstract string Description { get; }

    protected CommentStripOptions StripOptions { get; } = options ?? CommentStripOptions.Default;

    protected SourceProcessingMode Mode { get; } = mode;

    /// <summary>The strategy that answers for this language. Lexical scanning unless a grammar was registered.</summary>
    protected ICommentStripper Stripper { get; } = stripper ?? LexicalCommentStripper.Instance;

    protected ICommentStripObserver? Observer { get; } = observer;

    public string Minify(string sourceCode)
    {
        if (string.IsNullOrEmpty(sourceCode))
        {
            return sourceCode;
        }

        try
        {
            // 1. Remove comments. Literal contents are protected by the language syntax table, and a language with
            //    no entry is passed through untouched instead of being scanned with another language's rules.
            var stripped = Stripper.Strip(sourceCode, Language, StripOptions);
            Observer?.OnCommentsStripped(Language, stripped);

            if (!stripped.LanguageSupported)
            {
                return sourceCode;
            }

            // 2. Language-specific integrations that are not comment removal (for example C# region handling).
            var result = AfterCommentStrip(stripped.Content);

            if (Mode == SourceProcessingMode.StripComments)
            {
                return result;
            }

            // 3. Minification proper: language boilerplate, then whitespace-only lines, then the outer trim.
            result = MinifyWhitespace(result);
            result = BlankLineStripPattern.Replace(result, string.Empty);
            return result.Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            return sourceCode;
        }
    }

    /// <summary>Optional hook that runs in both modes, after comments are removed.</summary>
    protected virtual string AfterCommentStrip(string sourceCode) => sourceCode;

    /// <summary>Optional hook that runs only in <see cref="SourceProcessingMode.Minify"/>.</summary>
    protected virtual string MinifyWhitespace(string sourceCode) => sourceCode;
}
