namespace Plutus.Domain.Minifier;

/// <summary>
/// What a source-processing pass is allowed to change. The two modes are separate CLI flags on purpose:
/// <c>--strip-comments</c> edits only comments, <c>--minify</c> additionally collapses whitespace.
/// </summary>
public enum SourceProcessingMode
{
    /// <summary>
    /// Remove comments and nothing else. Whitespace, indentation and line endings stay byte for byte, so a
    /// stripped file still lines up with the original in a diff.
    /// </summary>
    StripComments,

    /// <summary>
    /// Remove comments, drop whitespace-only lines and trim the result, and apply language-specific boilerplate
    /// removal such as C# region directives.
    /// </summary>
    Minify,
}
