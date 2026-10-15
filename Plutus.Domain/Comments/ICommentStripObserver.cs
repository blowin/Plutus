namespace Plutus.Domain.Comments;

/// <summary>
/// Receives the outcome of every comment-stripping pass. Implemented by the run report so the selected strategy
/// and the resulting counts are observable instead of implicit.
/// </summary>
public interface ICommentStripObserver
{
    void OnCommentsStripped(LanguageId language, CommentStripResult result);
}
