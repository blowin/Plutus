using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Plutus.Domain.Structure;

namespace Plutus.Infrastructure.Structure;

/// <summary>
/// One implementation body that must be replaced, expressed as the inclusive token span that carries it together
/// with the text that replaces it.
/// </summary>
/// <param name="Kind">The declaration the body belonged to.</param>
/// <param name="Body">How the body was written.</param>
/// <param name="Span">The tokens to replace: the member from its first token through the closing brace or terminator.</param>
/// <param name="Replacement">
/// What takes their place: the member's own signature text (attributes, modifiers, name, parameters, constraints)
/// followed by the structural terminator and by any comment that stood between the signature and the body.
/// </param>
internal sealed record MemberBodyEdit(MemberKind Kind, BodyKind Body, TokenSpan Span, string Replacement);

/// <summary>
/// The C# parse-tree visitor behind signature extraction: a <see cref="CSharpSyntaxWalker"/> (the Roslyn equivalent
/// of <c>CSharpParserBaseVisitor</c>) that visits member declarations, translates each implementation body into a
/// token span, and stops descending once a body is claimed so nested members can never produce overlapping edits.
/// <para>
/// Two shapes are cut: a brace-delimited body and an <c>=&gt;</c> body. The span runs from the member's first token
/// to the end of its body, and the replacement is the member's signature text plus the terminator — so the result
/// is <c>public int Add(int a, int b);</c>, with attributes, documentation comments, generics and constraints
/// intact, rather than a semicolon stranded on a line of its own. A comment found between the signature and the
/// body is re-emitted after the terminator: it is never lost, and a line comment can never swallow the terminator.
/// </para>
/// <para>
/// Everything else — base lists, interface members, abstract members, auto-properties, field declarations and
/// anything nested inside a body that is being removed — is left untouched.
/// </para>
/// </summary>
internal sealed class ImplementationBodyVisitor(
    string source,
    IReadOnlyDictionary<int, int> indexByTokenStart,
    BodyTerminator terminator) : CSharpSyntaxWalker(SyntaxWalkerDepth.Node)
{
    private readonly List<MemberBodyEdit> _edits = [];

    public IReadOnlyList<MemberBodyEdit> Edits => _edits;

    private string Terminator { get; } = terminator.ToText();

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node) =>
        VisitMember(node, MemberKind.Method, node.Body, node.ExpressionBody, node.SemicolonToken);

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
        VisitMember(node, MemberKind.Constructor, node.Body, node.ExpressionBody, node.SemicolonToken);

    public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node) =>
        VisitMember(node, MemberKind.Destructor, node.Body, node.ExpressionBody, node.SemicolonToken);

    public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node) =>
        VisitMember(node, MemberKind.Operator, node.Body, node.ExpressionBody, node.SemicolonToken);

    public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) =>
        VisitMember(node, MemberKind.ConversionOperator, node.Body, node.ExpressionBody, node.SemicolonToken);

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) =>
        VisitMember(node, MemberKind.LocalFunction, node.Body, node.ExpressionBody, node.SemicolonToken);

    public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node) =>
        VisitMember(node, MemberKind.Accessor, node.Body, node.ExpressionBody, node.SemicolonToken);

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (node.ExpressionBody is { } arrow)
        {
            VisitMember(node, MemberKind.Property, null, arrow, node.SemicolonToken);
            return;
        }

        // An auto-property (get; set;) has no bodies at all; a property with accessor blocks is handled by the
        // accessor visits below, so this node is descended into rather than replaced.
        VisitChildren(node);
    }

    public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        if (node.ExpressionBody is { } arrow)
        {
            VisitMember(node, MemberKind.Indexer, null, arrow, node.SemicolonToken);
            return;
        }

        VisitChildren(node);
    }

    public override void VisitEventDeclaration(EventDeclarationSyntax node) => VisitChildren(node);

    private void VisitMember(
        SyntaxNode node,
        MemberKind kind,
        BlockSyntax? body,
        ArrowExpressionClauseSyntax? expressionBody,
        SyntaxToken semicolon)
    {
        if (body is not null && AddBlockBody(node, kind, body))
        {
            return;
        }

        if (expressionBody is not null && AddExpressionBody(node, kind, expressionBody, semicolon))
        {
            return;
        }

        // Nothing to cut here (an interface member, an abstract member, a declaration without a body), so the
        // member's own children are visited instead: nested types and accessor bodies still get their chance.
        VisitChildren(node);
    }

    private bool AddBlockBody(SyntaxNode node, MemberKind kind, BlockSyntax body)
    {
        // A body the parser synthesised while recovering from an error has no braces of its own. Cutting it would
        // delete real text, so it is skipped and the file is left as it is.
        if (body.OpenBraceToken.IsMissing || body.CloseBraceToken.IsMissing)
        {
            return false;
        }

        // The token before the body brace is where the signature ends; the comments that follow it belong to the
        // gap between the signature and the body, which this span removes and the replacement re-emits.
        var lastSignatureToken = body.OpenBraceToken.GetPreviousToken();
        return AddEdit(
            node,
            kind,
            BodyKind.Block,
            body.CloseBraceToken,
            lastSignatureToken,
            body.OpenBraceToken.LeadingTrivia);
    }

    private bool AddExpressionBody(SyntaxNode node, MemberKind kind, ArrowExpressionClauseSyntax arrow, SyntaxToken semicolon)
    {
        if (arrow.ArrowToken.IsMissing)
        {
            return false;
        }

        var endToken = semicolon.RawKind != (int)SyntaxKind.None && !semicolon.IsMissing
            ? semicolon
            : node.GetLastToken();

        return AddEdit(node, kind, BodyKind.ExpressionBody, endToken, arrow.ArrowToken.GetPreviousToken(), arrow.ArrowToken.LeadingTrivia);
    }

    /// <summary>
    /// Registers one replacement: everything from the member's first token (its attributes and documentation
    /// comments included) to the end of its body is replaced by the signature text, the terminator and the comments
    /// that separated the two.
    /// </summary>
    private bool AddEdit(
        SyntaxNode node,
        MemberKind kind,
        BodyKind bodyKind,
        SyntaxToken endToken,
        SyntaxToken lastSignatureToken,
        SyntaxTriviaList gapTrivia)
    {
        if (endToken.IsMissing || !IsUsable(lastSignatureToken))
        {
            return false;
        }

        var firstToken = node.GetFirstToken();
        if (!TryGetIndex(firstToken, out var first) || !TryGetIndex(endToken, out var end) || end < first)
        {
            return false;
        }

        var signatureEnd = lastSignatureToken.Span.End;
        if (signatureEnd < firstToken.FullSpan.Start || signatureEnd > firstToken.FullSpan.Start + source.Length)
        {
            return false;
        }

        var signature = source[firstToken.FullSpan.Start..signatureEnd];

        // The terminator replaces the body; the trailing trivia of the last token the span consumes is re-emitted
        // so the line break that ended the declaration is not lost with it. A comment in the gap between the
        // signature and the body is placed after the terminator, where it cannot swallow it.
        var replacement = signature
                          + Terminator
                          + CommentText(lastSignatureToken.TrailingTrivia, gapTrivia)
                          + endToken.TrailingTrivia.ToFullString();

        _edits.Add(new MemberBodyEdit(kind, bodyKind, new TokenSpan(first, end), replacement));
        return true;
    }

    /// <summary>
    /// Re-emits the comments that stood between the signature and the body. They follow the terminator so a line
    /// comment cannot swallow it, and consecutive comments are separated by a line break so the first cannot
    /// swallow the next.
    /// </summary>
    private static string CommentText(params SyntaxTriviaList[] triviaLists)
    {
        var comments = new List<string>();

        foreach (var triviaList in triviaLists)
        {
            foreach (var trivia in triviaList)
            {
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    comments.Add(trivia.ToFullString().TrimEnd('\r', '\n'));
                }
            }
        }

        return comments.Count == 0 ? string.Empty : " " + string.Join("\n", comments);
    }

    private static bool IsUsable(SyntaxToken token) => token.RawKind != (int)SyntaxKind.None && !token.IsMissing;

    /// <summary>
    /// Visits the children of a node without re-entering this node's own visit method, which is how a member that
    /// has nothing to cut still contributes the members nested inside it.
    /// </summary>
    private void VisitChildren(SyntaxNode node)
    {
        foreach (var child in node.ChildNodes())
        {
            Visit(child);
        }
    }

    private bool TryGetIndex(SyntaxToken token, out int index) => indexByTokenStart.TryGetValue(token.FullSpan.Start, out index);
}
