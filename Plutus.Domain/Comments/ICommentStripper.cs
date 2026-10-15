namespace Plutus.Domain.Comments;

/// <summary>
/// Removes comments from one language. <see cref="Strategy"/> states which mechanism answered, so a caller can
/// report it and a test can assert it without inspecting types.
/// </summary>
public interface ICommentStripper
{
    CommentStripStrategy Strategy { get; }

    CommentStripResult Strip(string source, LanguageId language, CommentStripOptions options);
}
