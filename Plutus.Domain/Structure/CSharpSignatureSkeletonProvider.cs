using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Plutus.Domain.Structure;

/// <summary>
/// Signature extraction for C#, backed by the Roslyn syntax tree: the file is parsed with the real C# parser, a
/// visitor turns every implementation body into a token span, and a <see cref="TokenStreamRewriter"/> replaces
/// those spans with the structural terminator.
/// <para>
/// This is an AST pass, not a text heuristic: what counts as a body is decided by the parse tree, so a
/// <c>{</c> inside a string, a lambda in a field initializer, or a verbatim string full of braces cannot be
/// mistaken for one. The provider is tolerant by construction — Roslyn recovers from syntax errors and the
/// diagnostic count is reported instead of the file being skipped.
/// </para>
/// </summary>
public sealed class CSharpSignatureSkeletonProvider : ISignatureSkeletonProvider
{
    public static readonly IReadOnlyCollection<MemberKind> MemberKinds = new[]
    {
        MemberKind.Method,
        MemberKind.Constructor,
        MemberKind.Destructor,
        MemberKind.Operator,
        MemberKind.ConversionOperator,
        MemberKind.Property,
        MemberKind.Indexer,
        MemberKind.Event,
        MemberKind.Accessor,
        MemberKind.LocalFunction,
    }.ToFrozenSet();

    public LanguageId Language => LanguageId.CSharp;

    public string Name => "roslyn-csharp";

    public IReadOnlyCollection<MemberKind> SupportedMemberKinds => MemberKinds;

    public SignatureExtractionResult Create(string source, SignatureExtractionOptions options)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest),
            path: "signature-skeleton.cs");

        var diagnostics = tree.GetDiagnostics();
        var diagnosticCount = diagnostics.Count();
        var root = tree.GetRoot();

        if (!RoslynTokenStream.TryCreate(root, source, out var projection, out var problem))
        {
            return Fallback(source, problem!, diagnosticCount);
        }

        var visitor = new ImplementationBodyVisitor(source, projection.IndexByTokenStart, options.Terminator);
        visitor.Visit(root);

        if (visitor.Edits.Count == 0)
        {
            return new SignatureExtractionResult
            {
                Content = source,
                Language = Language,
                Provider = Name,
                ParseDiagnostics = diagnosticCount,
            };
        }

        var rewriter = new TokenStreamRewriter(projection.Stream);
        try
        {
            foreach (var edit in visitor.Edits)
            {
                // The visitor owns the replacement text because it is the only place that knows which trivia the
                // span consumed; the rewriter still owns the safety checks.
                rewriter.Replace(edit.Span, edit.Replacement);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            // Conflicting edits mean the visitor and the tree disagree; leaving the file alone is the only safe
            // answer, and the reason reaches the run report.
            return Fallback(source, $"the parse tree produced conflicting body spans: {exception.Message}", diagnosticCount);
        }

        return new SignatureExtractionResult
        {
            Content = rewriter.GetRewrittenText(),
            Language = Language,
            Provider = Name,
            StrippedBodies = visitor.Edits
                .GroupBy(edit => edit.Kind)
                .ToFrozenDictionary(group => group.Key, group => group.Count()),
            ParseDiagnostics = diagnosticCount,
        };
    }

    private SignatureExtractionResult Fallback(string source, string reason, int diagnosticCount) => new()
    {
        Content = source,
        Language = Language,
        Provider = Name,
        LanguageSupported = true,
        ParseDiagnostics = diagnosticCount,
        FallbackReason = reason,
    };
}
