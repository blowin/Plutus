using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>Go minifier. Go has no distinct documentation comment form, so <c>//</c> is always an ordinary comment.</summary>
public sealed class GoLanguageMinifier(
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier("go", "Go Minifier (Removes comments, preserves standard and raw strings)", options, mode, stripper, observer);
