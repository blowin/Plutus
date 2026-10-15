using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// Style-sheet minifier. Plain CSS only has <c>/* */</c>; the preprocessor dialects add <c>//</c>, so the
/// language identifier decides which forms apply.
/// </summary>
public sealed class StyleLanguageMinifier(
    string language = "css",
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier(language, "Style Sheets Minifier (Removes CSS/SCSS standard and inline comment blocks)", options, mode, stripper, observer);
