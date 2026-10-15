using System.Text;
using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Structure;
using Plutus.Infrastructure.Diagnostics;

namespace Plutus.Test.Diagnostics;

/// <summary>
/// The run report is presentation, so it is tested as presentation: one place decides the wording, the alignment and
/// what is worth printing, and this is where that decision is pinned.
/// </summary>
public class RunReportWriterTests
{
    [Fact]
    public void Write_WithoutAnyRequestedProcessing_PrintsOnlyTheBundleSummary()
    {
        var output = new RecordingRunOutput();

        RunReportWriter.Write(output, Options(), Bundle(), Comments(), Signatures());

        output.Lines.Should().Equal(
            "Bundle created:  /tmp/out.md",
            "Files included:  12",
            "Total size:      2.4 KB",
            "Elapsed time:    0.42s (420 ms)");
        output.Warnings.Should().BeEmpty();
        output.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Write_WithoutABundle_PrintsNothing()
    {
        var output = new RecordingRunOutput();

        RunReportWriter.Write(output, Options(), new BundleRunReport(), Comments(), Signatures());

        output.Lines.Should().BeEmpty();
    }

    [Fact]
    public void Write_WithCommentProcessing_PrintsTheStrategyAndTheCounts()
    {
        var output = new RecordingRunOutput();

        RunReportWriter.Write(
            output,
            Options(commentPlan: Plan()),
            Bundle(),
            Comments(new CommentStrippingSummary
            {
                Request = CommentStrategyRequest.Auto,
                Strategy = CommentStripStrategy.Lexical,
                Rationale = "lexical scanning; no project-maintained grammar",
                Files = 12,
                RemovedInline = 20,
                RemovedBlock = 12,
                RemovedDocumentation = 5,
                PreservedDocumentation = 3,
                Languages = new Dictionary<LanguageId, long> { [LanguageId.CSharp] = 9, [LanguageId.JavaScript] = 3 },
            }),
            Signatures());

        output.Lines.Should().Equal(
            "Bundle created:  /tmp/out.md",
            "Files included:  12",
            "Total size:      2.4 KB",
            "Elapsed time:    0.42s (420 ms)",
            string.Empty,
            "Source processing",
            "  comments  strategy=lexical (requested: auto) — lexical scanning; no project-maintained grammar",
            "            files=12, removed=37 (inline 20, block 12, documentation 5), documentation preserved=3, languages: csharp=9, javascript=3");
    }

    [Fact]
    public void Write_WhenNothingNeededCommentProcessing_StillStatesTheStrategy()
    {
        var output = new RecordingRunOutput();

        RunReportWriter.Write(
            output,
            Options(commentPlan: Plan()),
            new BundleRunReport(),
            Comments(new CommentStrippingSummary
            {
                Request = CommentStrategyRequest.Lexical,
                Strategy = CommentStripStrategy.Lexical,
                Rationale = "explicit lexical scanning requested",
                Files = 0,
            }),
            Signatures());

        output.Lines.Should().Contain("  comments  strategy=lexical (requested: lexical) — explicit lexical scanning requested");
        output.Lines.Should().Contain("            no source file required comment processing");
    }

    [Fact]
    public void Write_WithSignatureExtraction_PrintsTheProviderAndTheCutBodies()
    {
        var output = new RecordingRunOutput();

        RunReportWriter.Write(
            output,
            Options(signatures: true),
            new BundleRunReport(),
            Comments(),
            Signatures(new SignatureExtractionSummary
            {
                Files = 12,
                BodiesByKind = new Dictionary<MemberKind, long>
                {
                    [MemberKind.Method] = 30,
                    [MemberKind.Property] = 9,
                    [MemberKind.Accessor] = 6,
                    [MemberKind.Constructor] = 3,
                },
                Providers = new Dictionary<string, long> { ["roslyn-csharp"] = 10 },
                FilesWithoutProvider = 2,
                ParseDiagnostics = 4,
                FilesWithParseDiagnostics = 1,
            }));

        output.Lines.Should().Contain("  signatures  provider=roslyn-csharp");
        output.Lines.Should().Contain(
            "              files=12, bodies=48 (accessor=6, constructor=3, method=30, property=9), without a provider=2, files with parse diagnostics=1");
    }

    [Fact]
    public void Write_WhenNothingNeededStructuralProcessing_SaysSo()
    {
        var output = new RecordingRunOutput();

        RunReportWriter.Write(output, Options(signatures: true), new BundleRunReport(), Comments(), Signatures());

        output.Lines.Should().Contain("  signatures  provider=none");
        output.Lines.Should().Contain("              no source file required structural processing");
    }

    [Fact]
    public void Write_WithFallbacks_ReportsEachOneAsAWarning()
    {
        var output = new RecordingRunOutput();

        RunReportWriter.Write(
            output,
            Options(commentPlan: Plan(), signatures: true),
            Bundle(),
            Comments(Comments() with { FallbackReasons = ["sql: lexer output does not tile the input"] }),
            Signatures(Signatures() with { FallbackReasons = ["rust: provider declined"] }));

        output.Warnings.Should().Equal(
            "Warning: comment stripping did not run for sql: lexer output does not tile the input",
            "Warning: signature extraction did not run for rust: provider declined");
    }

    [Fact]
    public void Write_UsesInvariantNumbers_SoTheReportDoesNotFollowTheMachineCulture()
    {
        var output = new RecordingRunOutput();
        var bundle = new BundleRunReport();
        bundle.Record("/tmp/out.md", 1234567, 2_500, TimeSpan.FromMilliseconds(420.5));

        RunReportWriter.Write(output, Options(), bundle, Comments(), Signatures());

        output.Lines.Should().Contain("Files included:  1234567");
        output.Lines.Should().Contain("Elapsed time:    0.42s (420 ms)");
    }

    [Fact]
    public void ConsoleRunOutput_FramesWarningsAndErrorsAndKeepsResultsOnStandardOutput()
    {
        var standard = new StringWriter();
        var error = new StringWriter();
        var output = new ConsoleRunOutput(standard, error);

        output.Info("bundle written");
        output.Warning("something was skipped");
        output.Error("something failed");

        standard.ToString().Should().Be($"bundle written{Environment.NewLine}");
        error.ToString().Should().Be(
            $"Warning: something was skipped{Environment.NewLine}Error: something failed{Environment.NewLine}");
    }

    [Fact]
    public void NullRunOutput_DiscardsEverything()
    {
        var act = () =>
        {
            NullRunOutput.Instance.Info("ignored");
            NullRunOutput.Instance.Warning("ignored");
            NullRunOutput.Instance.Error("ignored");
        };

        act.Should().NotThrow();
    }

    private static BundleRunReport Bundle()
    {
        var report = new BundleRunReport();
        report.Record("/tmp/out.md", 12, 2_500, TimeSpan.FromMilliseconds(420));
        return report;
    }

    /// <summary>An inactive comment summary: the pass was requested, but no file needed it.</summary>
    private static CommentStrippingSummary Comments(CommentStrippingSummary? summary = null) => summary ?? new()
    {
        Request = CommentStrategyRequest.Auto,
        Strategy = CommentStripStrategy.Lexical,
        Rationale = "lexical scanning",
        Files = 0,
    };

    /// <summary>An inactive structural summary: the pass was requested, but no file needed it.</summary>
    private static SignatureExtractionSummary Signatures(SignatureExtractionSummary? summary = null) => summary ?? new();

    private static CommentStrippingPlan Plan() => new()
    {
        Request = CommentStrategyRequest.Auto,
        DefaultStrategy = CommentStripStrategy.Lexical,
        Rationale = "lexical scanning; no project-maintained grammar",
    };

    private static RunAppOptions Options(CommentStrippingPlan? commentPlan = null, bool signatures = false) => new()
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
        CommentStripPlan = commentPlan,
        SignaturesOnly = signatures,
    };
}
