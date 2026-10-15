namespace Plutus.Domain.Comments;

/// <summary>
/// Classifies a removed comment. The distinction matters because documentation comments are the only kind a
/// user can ask to keep (see <see cref="CommentStripOptions.PreserveDocumentationComments"/>).
/// </summary>
public enum CommentKind
{
    /// <summary>A comment that ends with the line, such as <c>// text</c> or <c># text</c>.</summary>
    Inline,

    /// <summary>A delimited comment that may span lines, such as <c>/* text */</c> or <c>&lt;!-- text --&gt;</c>.</summary>
    Block,

    /// <summary>
    /// A comment a toolchain documents source with, such as <c>///</c>, <c>/** */</c> or <c>/*! */</c>.
    /// Languages without a distinct documentation form have none, and that is reported by the syntax table
    /// rather than guessed.
    /// </summary>
    Documentation,
}
