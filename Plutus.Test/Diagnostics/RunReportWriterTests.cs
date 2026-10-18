using System.Text;
using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Configuration;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Structure;
using Plutus.Infrastructure.Diagnostics;

namespace Plutus.Test.Diagnostics;

/// <summary>
/// The run report is presentation, so it is tested as presentation: one service decides the wording, the alignment
/// and what is worth printing, and it is built here with a recording output and summary data — no container needed.
/// </summary>
public class RunReportWriterTests
{
    [Fact]
    public void Write_WithoutAnyRequestedProcessing_PrintsOnlyTheBundleSummary()
    {
        var output = new RecordingRunOutput();

        Writer(output, Options(), Bundle()).Write();

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

        Writer(output, Options(), new BundleRunReport()).Write();

        output.Lines.Should().BeEmpty();
    }

    [Fact]
    public void Write_WithAProfile_StatesWhichKeysItSuppliedAndWhichTheCommandLineReplaced()
    {
        var output = new RecordingRunOutput();
        var options = Options() with
        {
            Profile = new ProfileProvenance
            {
                Path = "/repo/.plutusrc",
                AppliedKeys = ["excludeDirs", "includeExtensions"],
                OverriddenKeys = ["format"],
            },
        };

        Writer(output, options, Bundle()).Write();

        output.Lines.Should().Equal(
            string.Empty,
            "Configuration",
            "  profile  /repo/.plutusrc — applied: excludeDirs, includeExtensions; overridden by the command line: format",
            "Bundle created:  /tmp/out.md",
            "Files included:  12",
            "Total size:      2.4 KB",
            "Elapsed time:    0.42s (420 ms)");
    }

    [Fact]
    public void Write_WithAProfileThatRestatesDefaults_CountsThemInsteadOfListingThem()
    {
        // A generated profile states every key, so listing the ones that merely repeat the default would bury the
        // setting that actually decided the run.
        var output = new RecordingRunOutput();
        var options = Options() with
        {
            Profile = new ProfileProvenance
            {
                Path = "/repo/.plutusrc",
                AppliedKeys = ["excludeDirs"],
                DefaultedKeys = ["format", "encoding", "maxFileSize"],
                OverriddenKeys = ["output"],
            },
        };

        Writer(output, options, Bundle()).Write();

        output.Lines.Should().Contain(
            "  profile  /repo/.plutusrc — applied: excludeDirs; restated defaults: 3; overridden by the command line: output");
    }

    [Fact]
    public void Write_WithAProfileWhoseKeysWereAllReplaced_SaysSo()
    {
        var output = new RecordingRunOutput();
        var options = Options() with
        {
            Profile = new ProfileProvenance { Path = "/repo/.plutusrc", OverriddenKeys = ["format"] },
        };

        Writer(output, options, Bundle()).Write();

        output.Lines.Should().Contain("  profile  /repo/.plutusrc — overridden by the command line: format");
    }

    [Fact]
    public void Write_WithoutAProfile_PrintsNoConfigurationBlock()
    {
        var output = new RecordingRunOutput();

        Writer(output, Options(), Bundle()).Write();

        output.Lines.Should().NotContain("Configuration");
    }

    [Fact]
    public void Write_WithCommentProcessing_PrintsTheStrategyAndTheCounts()
    {
        var output = new RecordingRunOutput();
        var comments = InactiveComments() with
        {
            Files = 12,
            RemovedInline = 20,
            RemovedBlock = 12,
            RemovedDocumentation = 5,
            PreservedDocumentation = 3,
            Rationale = "lexical scanning; no project-maintained grammar",
            Languages = new Dictionary<LanguageId, long> { [LanguageId.CSharp] = 9, [LanguageId.JavaScript] = 3 },
        };

        Writer(output, Options(commentPlan: Plan()), Bundle(), comments).Write();

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
        var comments = InactiveComments() with
        {
            Request = CommentStrategyRequest.Lexical,
            Rationale = "explicit lexical scanning requested",
        };

        Writer(output, Options(commentPlan: Plan()), new BundleRunReport(), comments).Write();

        output.Lines.Should().Contain("  comments  strategy=lexical (requested: lexical) — explicit lexical scanning requested");
        output.Lines.Should().Contain("            no source file required comment processing");
    }

    [Fact]
    public void Write_WithSignatureExtraction_PrintsTheProviderAndTheCutBodies()
    {
        var output = new RecordingRunOutput();
        var signatures = new SignatureExtractionSummary
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
        };

        Writer(output, Options(signatures: true), new BundleRunReport(), signatures: signatures).Write();

        output.Lines.Should().Contain("  signatures  provider=roslyn-csharp");
        output.Lines.Should().Contain(
            "              files=12, bodies=48 (accessor=6, constructor=3, method=30, property=9), without a provider=2, files with parse diagnostics=1");
    }

    [Fact]
    public void Write_WhenNothingNeededStructuralProcessing_SaysSo()
    {
        var output = new RecordingRunOutput();

        Writer(output, Options(signatures: true), new BundleRunReport()).Write();

        output.Lines.Should().Contain("  signatures  provider=none");
        output.Lines.Should().Contain("              no source file required structural processing");
    }

    [Fact]
    public void Write_WithFallbacks_ReportsEachOneAsAWarning()
    {
        var output = new RecordingRunOutput();
        var comments = InactiveComments() with { FallbackReasons = ["sql: lexer output does not tile the input"] };
        var signatures = InactiveSignatures() with { FallbackReasons = ["rust: provider declined"] };

        Writer(output, Options(commentPlan: Plan(), signatures: true), Bundle(), comments, signatures).Write();

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

        Writer(output, Options(), bundle).Write();

        output.Lines.Should().Contain("Files included:  1234567");
        output.Lines.Should().Contain("Elapsed time:    0.42s (420 ms)");
    }

    [Fact]
    public void ConsoleRunOutput_FramesWarningsAndErrorsAndKeepsResultsOnStandardOutput()
    {
        var standard = new StringWriter();
        var error = new StringWriter();
        var output = new TextWriterRunOutput(standard, error);

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

    /// <summary>Builds the writer the way a host does, with summary data instead of live reports.</summary>
    private static RunReportWriter Writer(
        RecordingRunOutput output,
        RunAppOptions options,
        BundleRunReport bundle,
        CommentStrippingSummary? comments = null,
        SignatureExtractionSummary? signatures = null) =>
        new(output, options, bundle, comments ?? InactiveComments(), signatures ?? InactiveSignatures());

    private static BundleRunReport Bundle()
    {
        var report = new BundleRunReport();
        report.Record("/tmp/out.md", 12, 2_500, TimeSpan.FromMilliseconds(420));
        return report;
    }

    /// <summary>An inactive comment summary: the pass was requested, but no file needed it.</summary>
    private static CommentStrippingSummary InactiveComments() => new()
    {
        Request = CommentStrategyRequest.Auto,
        Strategy = CommentStripStrategy.Lexical,
        Rationale = "lexical scanning",
        Files = 0,
    };

    /// <summary>An inactive structural summary: the pass was requested, but no file needed it.</summary>
    private static SignatureExtractionSummary InactiveSignatures() => new();

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
