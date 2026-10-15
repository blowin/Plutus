using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>Java minifier, including Javadoc and text blocks.</summary>
public sealed class JavaLanguageMinifier(
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier("java", "Java Minifier (Removes standard block and inline comments)", options, mode, stripper, observer);
