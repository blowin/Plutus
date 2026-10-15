using System.Text;
using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Minifier;

namespace Plutus.Test.Comments;

/// <summary>
/// Pins the interaction of <c>--strip-comments</c>, <c>--minify</c> and <c>--preserve-doc-comments</c>, which is
/// the part of the feature a user cannot see from the flags alone.
/// </summary>
public class CommentStrippingInteractionTests
{
    private const string MixedSource = "a();\n// comment\n/// docs\nb();\n";

    [Fact]
    public void Create_NoCommentFlags_ReturnsTheIdentityMinifier()
    {
        var minifier = CodeMinifierFactory.Create(Options());

        minifier.Should().BeSameAs(IdentityCodeMinifier.Instance);
        minifier.Minify(MixedSource, "csharp").Should().Be(MixedSource);
    }

    [Fact]
    public void Create_StripComments_RemovesOnlyTheComments()
    {
        var minifier = CodeMinifierFactory.Create(Options(strip: true));

        // Every line ending survives, including the ones the comments left behind.
        minifier.Minify(MixedSource, "csharp").Should().Be("a();\n\n\nb();\n");
    }

    [Fact]
    public void Create_Minify_AlsoCollapsesWhitespaceOnlyLinesAndTrims()
    {
        var minifier = CodeMinifierFactory.Create(Options(minify: true));

        minifier.Minify(MixedSource, "csharp").Should().Be("a();\nb();");
    }

    [Fact]
    public void Create_BothFlags_BehavesLikeMinify()
    {
        var both = CodeMinifierFactory.Create(Options(minify: true, strip: true));
        var minifyOnly = CodeMinifierFactory.Create(Options(minify: true));

        both.Minify(MixedSource, "csharp").Should().Be(minifyOnly.Minify(MixedSource, "csharp"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Create_PreserveDocComments_KeepsDocumentationInBothModes(bool minify, bool strip)
    {
        var minifier = CodeMinifierFactory.Create(Options(minify: minify, strip: strip, preserve: true));

        var result = minifier.Minify(MixedSource, "csharp");

        result.Should().Contain("/// docs");
        result.Should().NotContain("// comment");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_DocumentationComments_AreRemovedByDefault(bool minify)
    {
        var minifier = CodeMinifierFactory.Create(Options(minify: minify, strip: !minify));

        minifier.Minify(MixedSource, "csharp").Should().NotContain("/// docs");
    }

    [Fact]
    public void Create_UnknownLanguage_IsReturnedUnchangedInBothModes()
    {
        const string source = "value # text\n/* neither */\n";

        CodeMinifierFactory.Create(Options(strip: true)).Minify(source, "lua").Should().Be(source);
        CodeMinifierFactory.Create(Options(minify: true)).Minify(source, "lua").Should().Be(source);
    }

    [Fact]
    public void Create_ReportsTheStrategyAndTheCountsItUsed()
    {
        var plan = Resolve(CommentStrategyRequest.Auto, GrammarInventory.Empty);
        var report = new CommentStrippingReport(plan);
        var options = Options(strip: true, plan: plan);
        var minifier = CodeMinifierFactory.Create(options, Context(options, plan, report));

        minifier.Minify("// comment\ncode();\n", "csharp");

        var summary = report.Snapshot();

        summary.IsActive.Should().BeTrue();
        summary.Files.Should().Be(1);
        summary.RemovedComments.Should().Be(1);
        summary.Strategies[LanguageId.CSharp].Should().Be(CommentStripStrategy.Lexical);
        summary.Languages[LanguageId.CSharp].Should().Be(1);
    }

    [Fact]
    public void Create_WithAntlrPlan_UsesTheTokenChannelStripperForThatLanguageOnly()
    {
        var inventory = GrammarInventory.Parse(
            """
            {
              "grammars": [
                {
                  "language": "sql",
                  "grammarFile": "SqlLexer.g4",
                  "lexerClass": "SqlLexer",
                  "commentTokenTypes": ["SIMPLE_COMMENT"],
                  "commentChannels": [1, "HIDDEN"]
                }
              ]
            }
            """);

        var plan = Resolve(CommentStrategyRequest.Antlr, inventory);
        var report = new CommentStrippingReport(plan);
        var lexer = new StubChannelLexer(LanguageId.Sql);
        var options = Options(strip: true, plan: plan);
        var minifiers = CompositeCodeMinifier.CreateFull(Context(
            options,
            plan,
            report,
            inventory,
            new Dictionary<LanguageId, ICommentChannelLexer> { [LanguageId.Sql] = lexer }));

        minifiers.Minify("SELECT 1; -- comment", LanguageId.Sql).Should().Be("SELECT 1; ");
        minifiers.Minify("a(); // comment", LanguageId.CSharp).Should().Be("a(); ");

        var summary = report.Snapshot();
        summary.Strategies[LanguageId.Sql].Should().Be(CommentStripStrategy.AntlrTokenChannel);
        summary.Strategies[LanguageId.CSharp].Should().Be(CommentStripStrategy.Lexical);
    }

    private static SourceProcessingContext Context(
        RunAppOptions options,
        CommentStrippingPlan plan,
        ICommentStripObserver? observer = null,
        GrammarInventory? inventory = null,
        IReadOnlyDictionary<LanguageId, ICommentChannelLexer>? lexers = null) =>
        CodeMinifierFactory.CreateContext(options, plan, inventory, lexers, observer);

    private static RunAppOptions Options(bool minify = false, bool strip = false, bool preserve = false, CommentStrippingPlan? plan = null) => new()
    {
        ProjectPath = ["."],
        OutputPath = null,
        UseGitignore = true,
        ExcludePatterns = [],
        UseDefaultExcludes = true,
        AllowDangerous = false,
        MaxFileSize = FileSize.Parse("1M"),
        ExcludeDirPatterns = [],
        ExcludeFilePatterns = [],
        IncludeDirPatterns = [],
        IncludeFilePatterns = [],
        TargetEncoding = Encoding.UTF8,
        MaxConcurrency = 1,
        Format = "md",
        Branch = null,
        Commit = null,
        Minify = minify,
        StripComments = strip,
        PreserveDocumentationComments = preserve,
        CommentStripPlan = plan,
    };

    private static CommentStrippingPlan Resolve(CommentStrategyRequest request, GrammarInventory inventory)
    {
        CommentStrippingPlanner.TryResolve(request, inventory, out var plan, out var error).Should().BeTrue(error);
        return plan;
    }

    /// <summary>
    /// A minimal lexer that reports one line comment per <c>--</c>, which is enough to prove that the channel path
    /// is wired and observable.
    /// </summary>
    private sealed class StubChannelLexer(LanguageId language) : ICommentChannelLexer
    {
        public LanguageId Language { get; } = language;

        public IReadOnlyList<CommentChannelToken> Tokenize(string source)
        {
            var tokens = new List<CommentChannelToken>();
            var position = 0;

            while (position < source.Length)
            {
                var comment = source.IndexOf("--", position, StringComparison.Ordinal);
                if (comment < 0)
                {
                    tokens.Add(new CommentChannelToken(1, "CODE", source[position..], 0, position));
                    break;
                }

                if (comment > position)
                {
                    tokens.Add(new CommentChannelToken(1, "CODE", source[position..comment], 0, position));
                }

                var lineEnd = source.IndexOf('\n', comment);
                var end = lineEnd < 0 ? source.Length : lineEnd;
                tokens.Add(new CommentChannelToken(2, "SIMPLE_COMMENT", source[comment..end], 1, comment));
                position = end;
            }

            return tokens;
        }
    }
}
