using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public abstract class BaseLanguageMinifier : ILanguageMinifier
{
    public abstract string Language { get; }
    public abstract string Description { get; }

    // Abstract property that concrete languages must implement
    protected abstract Regex CommentReplacePattern { get; }

    // Matches any line containing only optional whitespace followed by a newline character sequence.
    // The (?m) flag enables multi-line mode so ^ anchors to the start of individual lines.
    private static readonly Regex BlankLineStripPattern = new(@"(?m)^[ \t]*\r?\n", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    public string Minify(string sourceCode)
    {
        if (string.IsNullOrEmpty(sourceCode))
        {
            return sourceCode;
        }

        try
        {
            // 1. Strip comments out while leaving protected string boundaries unharmed
            var result = CommentReplacePattern.Replace(sourceCode, match =>
                match.Groups[1].Success ? match.Groups[1].Value : string.Empty);

            // 2. Run custom pipeline integrations (like C# region handling hooks)
            result = MinifyInternal(result);

            // 3. Remove all lines that contain only whitespace characters or are entirely empty.
            // This acts as an aggressive token compressor, pulling code lines tightly together.
            result = BlankLineStripPattern.Replace(result, string.Empty);

            return result.Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            return sourceCode;
        }
    }

    // Optional lifecycle hook for advanced language-specific stripping
    protected virtual string MinifyInternal(string sourceCode) => sourceCode;
}
