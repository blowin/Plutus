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
        CommentStrippingPlanner.TryResolve(CommentStrategyRequest.Auto, GrammarInventory.Empty, out var plan, out var error)
            .Should().BeTrue();

        error.Should().BeNull();
        plan.DefaultStrategy.Should().Be(CommentStripStrategy.Lexical);
        plan.GrammarBackedLanguages.Should().BeEmpty();
        plan.Rationale.Should().Contain("no project-maintained grammar");
        plan.Describe().Should().StartWith("strategy=lexical (requested: auto;");
    }

    [Fact]
    public void Resolve_ExplicitLexical_SaysItWasRequested()
    {
        CommentStrippingPlanner.TryResolve(CommentStrategyRequest.Lexical, GrammarInventory.Empty, out var plan, out _)
            .Should().BeTrue();

        plan.DefaultStrategy.Should().Be(CommentStripStrategy.Lexical);
        plan.Rationale.Should().Contain("explicit");
    }

    [Fact]
    public void Resolve_AntlrWithoutGrammars_IsAnError()
    {
        var resolved = CommentStrippingPlanner.TryResolve(CommentStrategyRequest.Antlr, GrammarInventory.Empty, out _, out var error);

        resolved.Should().BeFalse();
        error.Should().Contain("requires at least one project-maintained ANTLR grammar");
        error.Should().Contain("--comment-strategy lexical");
    }

    [Fact]
    public void Resolve_AntlrWithAGrammar_SelectsTokenChannels()
    {
        var inventory = GrammarInventory.Parse(SqlGrammarManifest);

        CommentStrippingPlanner.TryResolve(CommentStrategyRequest.Antlr, inventory, out var plan, out var error)
            .Should().BeTrue();

        error.Should().BeNull();
        plan.DefaultStrategy.Should().Be(CommentStripStrategy.AntlrTokenChannel);
        plan.GrammarBackedLanguages.Should().Equal(LanguageId.Sql);
        plan.Describe().Should().StartWith("strategy=antlr-token-channel");
    }

    [Fact]
    public void Resolve_AutoWithAGrammar_KeepsLexicalForEverythingElseButListsTheGrammar()
    {
        var inventory = GrammarInventory.Parse(SqlGrammarManifest);

        CommentStrippingPlanner.TryResolve(CommentStrategyRequest.Auto, inventory, out var plan, out _).Should().BeTrue();

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

        var resolved = CommentStrippingPlanner.TryResolve(request, inventory, out _, out var error);

        resolved.Should().BeFalse();
        error.Should().Contain("grammar inventory cannot be used");
    }

    [Fact]
    public void Resolve_BrokenManifest_StillAllowsExplicitLexicalScanning()
    {
        var inventory = GrammarInventory.Parse("{ not json");

        CommentStrippingPlanner.TryResolve(CommentStrategyRequest.Lexical, inventory, out var plan, out var error)
            .Should().BeTrue();

        error.Should().BeNull();
        plan.DefaultStrategy.Should().Be(CommentStripStrategy.Lexical);
    }

    [Fact]
    public void ToOptions_CarriesTheRequestedStrategyAndTheDocumentationChoice()
    {
        CommentStrippingPlanner.TryResolve(CommentStrategyRequest.Lexical, GrammarInventory.Empty, out var plan, out _).Should().BeTrue();

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
    public void Describe_WithoutActivity_StatesThatNothingNeededProcessing()
    {
        var report = new CommentStrippingReport(Plan());

        report.IsActive.Should().BeFalse();
        report.Describe().Should().Contain("no source file required comment processing");
    }

    [Fact]
    public void Records_AggregateCountsAcrossFilesAndLanguages()
    {
        var report = new CommentStrippingReport(Plan());

        report.OnCommentsStripped(LanguageId.CSharp, Result(LanguageId.CSharp, inline: 2, block: 1, documentation: 1));
        report.OnCommentsStripped(LanguageId.JavaScript, Result(LanguageId.JavaScript, inline: 3));

        report.IsActive.Should().BeTrue();
        report.FilesProcessed.Should().Be(2);
        report.RemovedComments.Should().Be(7);
        report.StrategiesByLanguage.Should().HaveCount(2);
    }

    [Fact]
    public void Describe_ReportsStrategyCountsAndLanguages()
    {
        var report = new CommentStrippingReport(Plan());
        report.OnCommentsStripped(LanguageId.CSharp, Result(LanguageId.CSharp, inline: 1));
        report.OnCommentsStripped(LanguageId.CSharp, Result(LanguageId.CSharp, block: 1, preservedDocumentation: 2, unterminated: 1));

        var description = report.Describe();

        description.Should().Contain("strategy=lexical");
        description.Should().Contain("files=2");
        description.Should().Contain("removed=2");
        description.Should().Contain("inline 1, block 1, documentation 0");
        description.Should().Contain("documentation preserved=2");
        description.Should().Contain("unterminated=1");
        description.Should().Contain("languages: csharp=2");
    }

    [Fact]
    public void Fallbacks_AreRecordedWithTheirReason()
    {
        var report = new CommentStrippingReport(Plan());

        report.OnCommentsStripped(LanguageId.Sql, Result(LanguageId.Sql) with { FallbackReason = "lexer output does not tile the input" });

        report.Fallbacks.Should().Be(1);
        report.FallbackReasons.Should().ContainSingle().Which.Should().Contain("does not tile");
        report.Describe().Should().Contain("strategy fallbacks=1");
    }

    private static CommentStrippingPlan Plan()
    {
        CommentStrippingPlanner.TryResolve(CommentStrategyRequest.Auto, GrammarInventory.Empty, out var plan, out _).Should().BeTrue();
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
