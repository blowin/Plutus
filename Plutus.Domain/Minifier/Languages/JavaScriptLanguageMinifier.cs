using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// JavaScript/TypeScript minifier. The lexical scanner protects template literals (including comments inside
/// <c>${...}</c> holes, which are code) and regular-expression literals such as <c>/[/*]/</c>.
/// </summary>
public sealed class JavaScriptLanguageMinifier(
    string language = "javascript",
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier(language, "JavaScript/TypeScript Minifier (Removes standard comments, protects template literals)", options, mode, stripper, observer);
