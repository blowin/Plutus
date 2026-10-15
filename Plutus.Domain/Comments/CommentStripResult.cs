namespace Plutus.Domain.Comments;

/// <summary>
/// What one comment-stripping pass did to one file. Counts are reported so the run can state which strategy was
/// used and how much it removed instead of asking the reader to trust it.
/// </summary>
public sealed record CommentStripResult
{
    /// <summary>The transformed source, or the input unchanged when the language is not supported.</summary>
    public required string Content { get; init; }

    /// <summary>The strategy that produced <see cref="Content"/>.</summary>
    public required CommentStripStrategy Strategy { get; init; }

    /// <summary>
    /// <c>false</c> when the language has no maintained comment syntax: the content is passed through untouched
    /// rather than having another language's rules applied to it.
    /// </summary>
    public bool LanguageSupported { get; init; } = true;

    public int RemovedInlineComments { get; init; }

    public int RemovedBlockComments { get; init; }

    public int RemovedDocumentationComments { get; init; }

    /// <summary>Documentation comments kept because <see cref="CommentStripOptions.PreserveDocumentationComments"/> was set.</summary>
    public int PreservedDocumentationComments { get; init; }

    /// <summary>
    /// Comment or literal openers that ran to the end of the file without their closing sequence. The scanner is
    /// deliberately tolerant (a compiler does the same) but the count is reported so a broken result is visible.
    /// </summary>
    public int UnterminatedSequences { get; init; }

    /// <summary>Set when a strategy declined to transform the file, with the reason it declined.</summary>
    public string? FallbackReason { get; init; }

    public int RemovedComments => RemovedInlineComments + RemovedBlockComments + RemovedDocumentationComments;
}
