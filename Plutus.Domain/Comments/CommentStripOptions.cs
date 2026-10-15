namespace Plutus.Domain.Comments;

/// <summary>
/// Controls what <c>--strip-comments</c> and <c>--minify</c> do with comments.
/// </summary>
public sealed record CommentStripOptions
{
    /// <summary>
    /// The defaults both CLI flags use: documentation comments are removed, exactly like any other comment.
    /// The default is explicit here so the behavior cannot drift with a language table change.
    /// </summary>
    public static readonly CommentStripOptions Default = new();

    public CommentStripOptions()
    {
    }

    public CommentStripOptions(CommentStrategyRequest request, bool preserveDocumentationComments, CommentStripStrategy strategy)
    {
        Request = request;
        PreserveDocumentationComments = preserveDocumentationComments;
        Strategy = strategy;
    }

    /// <summary>What the user asked for, kept for reporting. Defaults to <see cref="CommentStrategyRequest.Auto"/>.</summary>
    public CommentStrategyRequest Request { get; init; } = CommentStrategyRequest.Auto;

    /// <summary>
    /// When <c>true</c>, documentation comments (<c>///</c>, <c>//!</c>, <c>/** */</c>, <c>/*! */</c>) are kept
    /// verbatim. When <c>false</c> — the default — they are removed like any other comment.
    /// </summary>
    public bool PreserveDocumentationComments { get; init; }

    /// <summary>The resolved strategy. Lexical scanning unless a maintained grammar was selected.</summary>
    public CommentStripStrategy Strategy { get; init; } = CommentStripStrategy.Lexical;
}
