using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Plutus.Domain.Structure;

/// <summary>
/// Projects a Roslyn syntax tree into the language-neutral <see cref="CodeTokenStream"/> the rewriter works on.
/// Every token carries its trivia, so the stream tiles the source exactly and the only text a structural pass can
/// lose is the text inside the spans it deliberately replaces.
/// </summary>
internal static class RoslynTokenStream
{
    /// <summary>The projected stream plus the offset-to-index map the visitor uses to cut token spans.</summary>
    internal sealed record Projection(CodeTokenStream Stream, IReadOnlyDictionary<int, int> IndexByTokenStart);

    public static bool TryCreate(SyntaxNode root, string source, out Projection projection, out string? problem)
    {
        var tokens = new List<CodeToken>();
        var indexByStart = new Dictionary<int, int>();
        var index = 0;

        foreach (var token in root.DescendantTokens(descendIntoTrivia: false))
        {
            var text = token.ToFullString();
            tokens.Add(new CodeToken(index, Classify(token), text, token.FullSpan.Start));
            indexByStart[token.FullSpan.Start] = index;
            index++;
        }

        if (!CodeTokenStream.TryCreate(source, tokens, out var stream, out problem))
        {
            projection = null!;
            return false;
        }

        projection = new Projection(stream, indexByStart);
        return true;
    }

    private static TokenKind Classify(SyntaxToken token)
    {
        if (token.IsKind(SyntaxKind.EndOfFileToken))
        {
            return TokenKind.EndOfFile;
        }

        if (token.IsKind(SyntaxKind.IdentifierToken))
        {
            return TokenKind.Identifier;
        }

        if (SyntaxFacts.GetKeywordKind(token.Text) != SyntaxKind.None)
        {
            return TokenKind.Keyword;
        }

        return IsLiteral(token) ? TokenKind.Literal : TokenKind.Punctuation;
    }

    private static bool IsLiteral(SyntaxToken token) =>
        token.Parent is LiteralExpressionSyntax
        || token.Kind().ToString().EndsWith("LiteralToken", StringComparison.Ordinal);
}
