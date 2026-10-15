using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>PHP minifier, including heredoc and nowdoc bodies.</summary>
public sealed class PhpLanguageMinifier(
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier("php", "PHP Minifier (Removes //, # and /*...*/ comments)", options, mode, stripper, observer);
