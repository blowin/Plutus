using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>SQL minifier, including PostgreSQL dollar-quoted bodies and T-SQL bracket identifiers.</summary>
public sealed class SqlLanguageMinifier(
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier("sql", "SQL Minifier (Removes inline -- and block /*...*/ comments)", options, mode, stripper, observer);
