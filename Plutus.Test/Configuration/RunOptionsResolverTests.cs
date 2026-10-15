using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Configuration;
using Plutus.Domain.Tokens;

namespace Plutus.Test.Configuration;

/// <summary>
/// The merge rule is the whole point of a profile: an explicit flag wins, a profile key wins over the built-in
/// default, and a flag replaces the profile's value instead of being combined with it.
/// </summary>
public class RunOptionsResolverTests
{
    [Fact]
    public void Resolve_WithoutAProfile_ProducesTheBuiltInDefaults()
    {
        var resolution = new RunOptionsResolver().Resolve(CommandLine());

        resolution.Succeeded.Should().BeTrue(resolution.Error);
        var options = resolution.Options!;
        options.Format.Should().Be("md");
        options.TargetEncoding.WebName.Should().Be("utf-8");
        options.MaxConcurrency.Should().Be(8);
        options.MaxFileSize.Should().Be(FileSize.Parse("1M"));
        options.UseDefaultExcludes.Should().BeTrue();
        options.UseGitignore.Should().BeTrue();
        options.AllowDangerous.Should().BeFalse();
        options.IncludeFileContents.Should().BeTrue();
        options.ExcludeDirPatterns.Should().BeEmpty();
        options.IncludeFilePatterns.Should().BeEmpty();
        options.Profile.Should().BeNull("a run without a profile has nothing to report");
    }

    [Fact]
    public void Resolve_ProfileValues_ApplyWhenTheCommandLineSaysNothing()
    {
        var profile = new PlutusProfile
        {
            ExcludeDirs = ["bin", "obj", "node_modules"],
            IncludeExtensions = [".cs", ".ts"],
            Format = "json",
            MaxConcurrency = 2,
            UseGitignore = false,
            UseDefaultExcludes = false,
            AllowDangerousFiles = true,
            Output = "artifacts/context.md",
            MaxFileSize = "512K",
            StripComments = true,
            PreserveDocComments = true,
            IncludeFileContents = false,
        };

        var resolution = new RunOptionsResolver().Resolve(CommandLine(), profile, ProfilePath());

        resolution.Succeeded.Should().BeTrue(resolution.Error);
        var options = resolution.Options!;
        options.ExcludeDirPatterns.Should().Equal("bin", "obj", "node_modules");
        options.IncludeFilePatterns.Should().Equal("*.cs", "*.ts");
        options.Format.Should().Be("json");
        options.MaxConcurrency.Should().Be(2);
        options.UseGitignore.Should().BeFalse();
        options.UseDefaultExcludes.Should().BeFalse();
        options.AllowDangerous.Should().BeTrue();
        options.MaxFileSize.Should().Be(FileSize.Parse("512K"));
        options.StripComments.Should().BeTrue();
        options.PreserveDocumentationComments.Should().BeTrue();
        options.IncludeFileContents.Should().BeFalse();
        options.OutputPath.Should().Be(Path.Combine(Path.GetDirectoryName(ProfilePath())!, "artifacts/context.md"));
    }

    [Fact]
    public void Resolve_ExplicitFlags_OverrideTheProfile()
    {
        var profile = new PlutusProfile
        {
            ExcludeDirs = ["bin"],
            Format = "json",
            MaxConcurrency = 2,
            StripComments = true,
            Minify = true,
        };
        var request = CommandLine() with
        {
            ExcludeDirs = ["node_modules"],
            Format = "md",
            MaxConcurrency = 16,
            StripComments = false,
        };

        var options = new RunOptionsResolver().Resolve(request, profile, ProfilePath()).Options!;

        options.ExcludeDirPatterns.Should().Equal("node_modules");
        options.Format.Should().Be("md");
        options.MaxConcurrency.Should().Be(16);
        options.StripComments.Should().BeFalse("a flag can turn a profile setting off");
        options.Minify.Should().BeTrue("a key the command line did not mention still applies");
    }

    [Fact]
    public void Resolve_Provenance_SaysWhichKeysCameFromTheProfileAndWhichWereReplaced()
    {
        var profile = new PlutusProfile { ExcludeDirs = ["bin"], Format = "json", MaxConcurrency = 2 };
        var request = CommandLine() with { Format = "md", MaxFileSize = FileSize.Parse("64K") };

        var provenance = new RunOptionsResolver().Resolve(request, profile, ProfilePath()).Options!.Profile!;

        provenance.Path.Should().Be(ProfilePath());
        provenance.AppliedKeys.Should().Equal("excludeDirs", "maxConcurrency");
        provenance.OverriddenKeys.Should().Equal("format");
    }

    [Fact]
    public void Resolve_IncludeExtensions_AreNormalizedIntoInclusivePatterns()
    {
        var profile = new PlutusProfile { IncludeExtensions = ["cs", ".ts", "*.razor"] };

        var options = new RunOptionsResolver().Resolve(CommandLine(), profile, ProfilePath()).Options!;

        options.IncludeFilePatterns.Should().Equal("*.cs", "*.ts", "*.razor");
    }

    [Fact]
    public void Resolve_IncludeFilesAndExtensionsFromTheProfile_AreOneFilterList()
    {
        var profile = new PlutusProfile { IncludeFiles = ["src/**"], IncludeExtensions = [".cs"] };

        var options = new RunOptionsResolver().Resolve(CommandLine(), profile, ProfilePath()).Options!;

        options.IncludeFilePatterns.Should().Equal("src/**", "*.cs");
        options.Profile!.AppliedKeys.Should().Equal("includeFiles", "includeExtensions");
    }

    [Fact]
    public void Resolve_IncludeFilesFromTheCommandLine_ReplaceTheProfileFilters()
    {
        var profile = new PlutusProfile { IncludeFiles = ["src/**"], IncludeExtensions = [".cs"] };
        var request = CommandLine() with { IncludeFiles = ["tests/**"] };

        var options = new RunOptionsResolver().Resolve(request, profile, ProfilePath()).Options!;

        options.IncludeFilePatterns.Should().Equal("tests/**");
        options.Profile!.OverriddenKeys.Should().Equal("includeFiles", "includeExtensions");
    }

    [Fact]
    public void Resolve_TokenBudget_ComesFromTheProfile()
    {
        var profile = new PlutusProfile
        {
            MaxTokens = 9000,
            TokenOverflowPolicy = TokenOverflowPolicy.Select,
            TokenSelectionStrategy = TokenSelectionStrategy.LargestFirst,
        };

        var options = new RunOptionsResolver().Resolve(CommandLine(), profile, ProfilePath()).Options!;

        options.TokenBudget.Should().NotBeNull();
        options.TokenBudget!.MaxTokens.Should().Be(9000);
        options.TokenBudget.OverflowPolicy.Should().Be(TokenOverflowPolicy.Select);
        options.TokenBudget.SelectionStrategy.Should().Be(TokenSelectionStrategy.LargestFirst);
    }

    [Fact]
    public void Resolve_DiffScope_CarriesTheLimitsTheRunResolved()
    {
        // A diff cannot bypass the limits the content path uses, including the ones a profile set.
        var profile = new PlutusProfile { MaxFileSize = "256K", MaxTokens = 1000 };
        var request = CommandLine() with
        {
            Diff = new DiffRequest { From = "main", To = "feature", Only = true, IncludeUntracked = true },
            IncludeFileContents = false,
        };

        var options = new RunOptionsResolver().Resolve(request, profile, ProfilePath()).Options!;

        options.IncludeFileContents.Should().BeFalse();
        options.Diff.Should().NotBeNull();
        options.Diff!.Target.Should().Be("main");
        options.Diff.TargetTo.Should().Be("feature");
        options.Diff.IncludeUntracked.Should().BeTrue();
        options.Diff.MaxFileSize.Should().Be(FileSize.Parse("256K"));
        options.Diff.TokenBudget!.MaxTokens.Should().Be(1000);
    }

    [Fact]
    public void Resolve_CommentProcessing_ComesFromTheProfile()
    {
        var profile = new PlutusProfile
        {
            StripComments = true,
            PreserveDocComments = true,
            CommentStrategy = CommentStrategyRequest.Lexical,
            SignaturesOnly = true,
        };

        var options = new RunOptionsResolver().Resolve(CommandLine(), profile, ProfilePath()).Options!;

        options.StripComments.Should().BeTrue();
        options.Minify.Should().BeFalse();
        options.PreserveDocumentationComments.Should().BeTrue();
        options.CommentStrategy.Should().Be(CommentStrategyRequest.Lexical);
        options.SignaturesOnly.Should().BeTrue();
        options.Profile!.AppliedKeys.Should().Contain("commentStrategy");
    }

    [Fact]
    public void Resolve_AnEncodingThePlatformKnows_IsUsed()
    {
        var profile = new PlutusProfile { Encoding = "utf-8" };

        var options = new RunOptionsResolver().Resolve(CommandLine(), profile, ProfilePath()).Options!;

        options.TargetEncoding.WebName.Should().Be("utf-8");
        options.Profile!.DefaultedKeys.Should().Contain("encoding", "the profile restates the default encoding");
    }

    [Fact]
    public void Resolve_AKeyStatedAtItsDefault_IsReportedSeparatelyFromTheOnesThatDecidedTheRun()
    {
        // A starter profile states every key; only the ones that differ from the default decided anything, and the
        // report says which is which instead of burying the real settings in a list of restated defaults.
        var profile = new PlutusProfile
        {
            Format = "md",
            MaxConcurrency = 8,
            IncludeFileContents = true,
            ExcludeDirs = ["bin"],
            MaxFileSize = "2M",
        };

        var provenance = new RunOptionsResolver().Resolve(CommandLine(), profile, ProfilePath()).Options!.Profile!;

        provenance.AppliedKeys.Should().Equal("maxFileSize", "excludeDirs");
        provenance.DefaultedKeys.Should().Equal("maxConcurrency", "format", "includeFileContents");
        provenance.OverriddenKeys.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_AnEncodingThePlatformDoesNotKnow_IsReportedAgainstTheProfile()
    {
        var profile = new PlutusProfile { Encoding = "klingon-9" };

        var resolution = new RunOptionsResolver().Resolve(CommandLine(), profile, ProfilePath());

        resolution.Succeeded.Should().BeFalse();
        resolution.Error.Should().Contain("encoding").And.Contain("klingon-9");
    }

    [Fact]
    public void Resolve_AnExplicitPathAndProfileKey_AgreeOnTheCommandLineWinning()
    {
        var profile = new PlutusProfile { Output = "artifacts/from-profile.md" };
        var request = CommandLine() with { OutputPath = "explicit.md" };

        var options = new RunOptionsResolver().Resolve(request, profile, ProfilePath()).Options!;

        options.OutputPath.Should().Be("explicit.md", "a command-line path is relative to where the command runs");
        options.Profile!.OverriddenKeys.Should().Equal("output");
    }

    private static string ProfilePath() => Path.Combine(Path.GetTempPath(), "plutus-profile-tests", ".plutusrc");

    private static RunOptionsRequest CommandLine() => new()
    {
        ProjectPath = ["."],
    };
}
