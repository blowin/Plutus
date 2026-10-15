using System.Text;
using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Minifier;
using Plutus.Domain.Structure;
using Plutus.Infrastructure.Structure;

namespace Plutus.Test.Structure;

/// <summary>
/// The structural pass as the run sees it: which flag combination produces it, how it composes with comment
/// removal and minification, what happens for a language without a provider, and what the run reports.
/// </summary>
public class SignatureExtractionPipelineTests
{
    private const string ClassWithComment = """
        public class Sample
        {
            // a comment
            public int Add(int a, int b)
            {
                return a + b;
            }
        }
        """;

    [Fact]
    public void Create_WithoutAnyFlag_ReturnsTheIdentityMinifier()
    {
        TestSourceProcessing.Create(Options()).Should().BeOfType<IdentityCodeMinifier>();
    }

    [Fact]
    public void Create_WithSignaturesOnly_ReplacesBodiesAndLeavesCommentsAlone()
    {
        var minifier = TestSourceProcessing.Create(Options(signatures: true));

        var result = minifier.Minify(ClassWithComment, LanguageId.CSharp);

        result.Should().Contain("// a comment");
        result.Should().Contain("public int Add(int a, int b);");
        result.Should().NotContain("return a + b;");
    }

    [Fact]
    public void Create_WithSignaturesAndStripComments_RemovesCommentsFirstAndThenTheBodies()
    {
        var minifier = TestSourceProcessing.Create(Options(signatures: true, strip: true));

        var result = minifier.Minify(ClassWithComment, LanguageId.CSharp);

        result.Should().NotContain("// a comment");
        result.Should().Contain("public int Add(int a, int b);");
    }

    [Fact]
    public void Create_WithSignaturesAndMinify_CollapsesWhitespaceAfterTheStructuralPass()
    {
        var minifier = TestSourceProcessing.Create(Options(signatures: true, minify: true));

        var result = minifier.Minify("public class Sample\n{\n\n\n    public void M()\n    {\n        Work();\n    }\n}\n", LanguageId.CSharp);

        result.Should().NotContain("Work();");
        result.Should().Contain("public void M();");
        result.Should().NotContain("\n\n");
    }

    [Fact]
    public void Create_WithSignaturesOnly_LeavesALanguageWithoutAProviderAlone()
    {
        const string python = "def add(a, b):\n    return a + b\n";

        var minifier = TestSourceProcessing.Create(Options(signatures: true));

        minifier.Minify(python, "python").Should().Be(python);
    }

    [Fact]
    public void Create_WithSignaturesOnly_LeavesAnUnknownLanguageAlone()
    {
        const string source = "whatever this is\n";

        TestSourceProcessing.Create(Options(signatures: true)).Minify(source, "lua").Should().Be(source);
    }

    [Fact]
    public void Report_DescribesFilesProvidersAndStrippedKinds()
    {
        var report = new SignatureExtractionReport();
        var options = Options(signatures: true);
        var context = TestSourceProcessing.Factory(options, signatureObserver: report).CreateContext();
        var minifier = TestSourceProcessing.Minifier(context);

        minifier.Minify(ClassWithComment, LanguageId.CSharp);
        minifier.Minify("def add(a, b):\n    return a + b\n", "python");

        var summary = report.Snapshot();

        summary.IsActive.Should().BeTrue();
        summary.Files.Should().Be(2);
        summary.StrippedBodies.Should().Be(1);
        summary.FilesWithoutProvider.Should().Be(1);
        summary.Providers.Should().ContainKey("roslyn-csharp").WhoseValue.Should().Be(1);
        summary.BodiesByKind.Should().ContainKey(MemberKind.Method).WhoseValue.Should().Be(1);
        summary.Fallbacks.Should().Be(0);
    }

    [Fact]
    public void Report_WithoutActivity_SaysSo()
    {
        var summary = new SignatureExtractionReport().Snapshot();

        summary.IsActive.Should().BeFalse();
        summary.Files.Should().Be(0);
        summary.StrippedBodies.Should().Be(0);
    }

    [Fact]
    public void Extractor_WithoutAProviderForTheLanguage_ReportsItInsteadOfGuessing()
    {
        var extractor = new SignatureSkeletonExtractor(TestSourceProcessing.Providers());

        var result = extractor.Extract("def add(a, b):\n    return a + b\n", LanguageId.Python, SignatureExtractionOptions.Default);

        result.LanguageSupported.Should().BeFalse();
        result.Content.Should().Be("def add(a, b):\n    return a + b\n");
        result.FallbackReason.Should().Contain("python");
        extractor.Supports(LanguageId.CSharp).Should().BeTrue();
        extractor.SupportedLanguages.Should().Equal(LanguageId.CSharp);
    }

    [Fact]
    public void DiscoveredProviders_AreExactlyTheOnesThisBuildShips()
    {
        // Discovery is the only list of providers: scanning the infrastructure assembly is how the container finds
        // them, and how the tests and the documentation get the same set.
        var discovered = TestSourceProcessing.Providers();

        discovered.Should().ContainSingle();
        discovered[0].Language.Should().Be(LanguageId.CSharp);
        discovered[0].Name.Should().Be("roslyn-csharp");
    }

    [Fact]
    public void Extract_WithATerminatorOtherThanTheDefault_IsDrivenByOptions()
    {
        // The enum keeps the choice explicit; today ';' is the only supported terminator.
        SignatureExtractionOptions.Default.Terminator.Should().Be(BodyTerminator.Semicolon);
        SignatureExtractionOptions.Default.TerminatorText.Should().Be(";");
    }

    private static RunAppOptions Options(bool minify = false, bool strip = false, bool signatures = false) => new()
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
        SignaturesOnly = signatures,
    };
}
