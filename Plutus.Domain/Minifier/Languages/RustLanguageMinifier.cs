using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>Rust minifier. Block comments nest, and lifetimes are not mistaken for character literals.</summary>
public sealed class RustLanguageMinifier(
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier("rust", "Rust Minifier (Removes standard/doc comments)", options, mode, stripper, observer);
