namespace Plutus.Domain.Minifier;

/// <summary>
/// What a source-processing pass is allowed to change. The axes are independent on purpose: comments, whitespace
/// and structural signatures are three different user-visible promises, and a run can ask for any combination of
/// them.
/// </summary>
public enum SourceProcessingMode
{
    /// <summary>No comment or whitespace processing at all.</summary>
    None = 0,

    /// <summary>
    /// Remove comments and nothing else. Whitespace, indentation and line endings stay byte for byte, so a
    /// stripped file still lines up with the original in a diff.
    /// </summary>
    StripComments = 1,

    /// <summary>
    /// Remove comments, drop whitespace-only lines and trim the result, and apply language-specific boilerplate
    /// removal such as C# region directives.
    /// </summary>
    Minify = 2,
}
