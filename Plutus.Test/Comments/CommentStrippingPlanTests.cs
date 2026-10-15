using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;

namespace Plutus.Test.Comments;

/// <summary>
/// The planner is where "AST-driven" claims are either earned or refused: ANTLR token channels are selected only
/// for languages a maintained grammar covers, and asking for them without one fails instead of falling back.
/// </summary>
public class CommentStrippingPlanTests
{
    private const string SqlGrammarManifest = """
        {
          "grammars": [
            {
              "language": "sql",
              "grammarFile": "SqlLexer.g4",
              "commentTokenTypes": ["SIMPLE_COMMENT"],
              "commentChannels": ["HIDDEN"]
            }
          ]
        }
        """;

    [Fact]
    public void Resolve_AutoWithoutGrammars_ChoosesLexicalScanningAndSaysWhy()
    {
        new CommentStrippingPlanner().TryResolve(CommentStrategyRequest.Auto, GrammarInventory.Empty, out var plan, out var error)
            .Should().BeTrue();

        error.Should().BeNull();
        plan.DefaultStrategy.Should().Be(CommentStripStrategy.Lexical);
        plan.GrammarBackedLanguages.Should().BeEmpty();
        plan.Rationale.Should().Contain("no project-maintained grammar");
        plan.DefaultStrategy.ToDisplayName().Should().Be("lexical");
        plan.Request.ToDisplayName().Should().Be("auto");
    }

    [Fact]
    public void Resolve_ExplicitLexical_SaysItWasRequested()
    {
        new CommentStrippingPlanner().TryResolve(CommentStrategyRequest.Lexical, GrammarInventory.Empty, out var plan, out _)
            .Should().BeTrue();

        plan.DefaultStrategy.Should().Be(CommentStripStrategy.Lexical);
        plan.Rationale.Should().Contain("explicit");
    }

    [Fact]
    public void Resolve_AntlrWithoutGrammars_IsAnError()
    {
        var resolved = new CommentStrippingPlanner().TryResolve(CommentStrategyRequest.Antlr, GrammarInventory.Empty, out _, out var error);

        resolved.Should().BeFalse();
        error.Should().Contain("requires at least one project-maintained ANTLR grammar");
        error.Should().Contain("--comment-strategy lexical");
    }

    [Fact]
    public void Resolve_AntlrWithAGrammar_SelectsTokenChannels()
    {
        var inventory = GrammarInventory.Parse(SqlGrammarManifest);

        new CommentStrippingPlanner().TryResolve(CommentStrategyRequest.Antlr, inventory, out var plan, out var error)
            .Should().BeTrue();

        error.Should().BeNull();
        plan.DefaultStrategy.Should().Be(CommentStripStrategy.AntlrTokenChannel);
        plan.GrammarBackedLanguages.Should().Equal(LanguageId.Sql);
        plan.DefaultStrategy.ToDisplayName().Should().Be("antlr-token-channel");
    }

    [Fact]
    public void Resolve_AutoWithAGrammar_KeepsLexicalForEverythingElseButListsTheGrammar()
    {
        var inventory = GrammarInventory.Parse(SqlGrammarManifest);

        new CommentStrippingPlanner().TryResolve(CommentStrategyRequest.Auto, inventory, out var plan, out _).Should().BeTrue();

        plan.DefaultStrategy.Should().Be(CommentStripStrategy.Lexical);
        plan.GrammarBackedLanguages.Should().Equal(LanguageId.Sql);
        plan.Rationale.Should().Contain("ANTLR token channels for [sql]");
    }

    [Theory]
    [InlineData(CommentStrategyRequest.Auto)]
    [InlineData(CommentStrategyRequest.Antlr)]
    public void Resolve_BrokenManifest_FailsInsteadOfSilentlyDegrading(CommentStrategyRequest request)
    {
        var inventory = GrammarInventory.Parse("{ \"grammars\": [ { \"language\": \"sql\" } ] }");

        var resolved = new CommentStrippingPlanner().TryResolve(request, inventory, out _, out var error);

        resolved.Should().BeFalse();
        error.Should().Contain("grammar inventory cannot be used");
    }

    [Fact]
    public void Resolve_BrokenManifest_StillAllowsExplicitLexicalScanning()
    {
        var inventory = GrammarInventory.Parse("{ not json");

        new CommentStrippingPlanner().TryResolve(CommentStrategyRequest.Lexical, inventory, out var plan, out var error)
            .Should().BeTrue();

        error.Should().BeNull();
        plan.DefaultStrategy.Should().Be(CommentStripStrategy.Lexical);
    }

    [Fact]
    public void ToOptions_CarriesTheRequestedStrategyAndTheDocumentationChoice()
    {
        new CommentStrippingPlanner().TryResolve(CommentStrategyRequest.Lexical, GrammarInventory.Empty, out var plan, out _).Should().BeTrue();

        var options = plan.ToOptions(preserveDocumentationComments: true);

        options.Request.Should().Be(CommentStrategyRequest.Lexical);
        options.Strategy.Should().Be(CommentStripStrategy.Lexical);
        options.PreserveDocumentationComments.Should().BeTrue();
    }
}

/// <summary>The run report is what makes the strategy observable from outside the process.</summary>
public class CommentStrippingReportTests
{
    [Fact]
    public void Snapshot_WithoutActivity_ReportsThatNothingWasProcessed()
    {
        var report = new CommentStrippingReport(Plan());

        report.IsActive.Should().BeFalse();
        report.Snapshot().IsActive.Should().BeFalse();
        report.Snapshot().Files.Should().Be(0);
    }

    [Fact]
    public void Records_AggregateCountsAcrossFilesAndLanguages()
    {
        var report = new CommentStrippingReport(Plan());

        report.OnCommentsStripped(LanguageId.CSharp, Result(LanguageId.CSharp, inline: 2, block: 1, documentation: 1));
        report.OnCommentsStripped(LanguageId.JavaScript, Result(LanguageId.JavaScript, inline: 3));

        var summary = report.Snapshot();

        summary.IsActive.Should().BeTrue();
        summary.Files.Should().Be(2);
        summary.RemovedComments.Should().Be(7);
        summary.Languages.Should().HaveCount(2);
        summary.Languages[LanguageId.CSharp].Should().Be(1);
    }

    [Fact]
    public void Snapshot_CarriesStrategyCountsAndLanguages()
    {
        var report = new CommentStrippingReport(Plan());
        report.OnCommentsStripped(LanguageId.CSharp, Result(LanguageId.CSharp, inline: 1));
        report.OnCommentsStripped(LanguageId.CSharp, Result(LanguageId.CSharp, block: 1, preservedDocumentation: 2, unterminated: 1));

        var summary = report.Snapshot();

        summary.Strategy.Should().Be(CommentStripStrategy.Lexical);
        summary.Request.Should().Be(CommentStrategyRequest.Auto);
        summary.Rationale.Should().NotBeNullOrWhiteSpace();
        summary.Files.Should().Be(2);
        summary.RemovedComments.Should().Be(2);
        summary.RemovedInline.Should().Be(1);
        summary.RemovedBlock.Should().Be(1);
        summary.RemovedDocumentation.Should().Be(0);
        summary.PreservedDocumentation.Should().Be(2);
        summary.Unterminated.Should().Be(1);
        summary.Languages[LanguageId.CSharp].Should().Be(2);
        summary.Strategies[LanguageId.CSharp].Should().Be(CommentStripStrategy.Lexical);
    }

    [Fact]
    public void Fallbacks_AreRecordedWithTheirReason()
    {
        var report = new CommentStrippingReport(Plan());

        report.OnCommentsStripped(LanguageId.Sql, Result(LanguageId.Sql) with { FallbackReason = "lexer output does not tile the input" });

        var summary = report.Snapshot();

        summary.Fallbacks.Should().Be(1);
        summary.FallbackReasons.Should().ContainSingle().Which.Should().Contain("does not tile");
    }

    private static CommentStrippingPlan Plan()
    {
        new CommentStrippingPlanner().TryResolve(CommentStrategyRequest.Auto, GrammarInventory.Empty, out var plan, out _).Should().BeTrue();
        return plan;
    }

    private static CommentStripResult Result(
        LanguageId language,
        int inline = 0,
        int block = 0,
        int documentation = 0,
        int preservedDocumentation = 0,
        int unterminated = 0) => new()
        {
            Content = language.Value,
            Strategy = CommentStripStrategy.Lexical,
            Language = language,
            RemovedInlineComments = inline,
            RemovedBlockComments = block,
            RemovedDocumentationComments = documentation,
            PreservedDocumentationComments = preservedDocumentation,
            UnterminatedSequences = unterminated,
        };
}
