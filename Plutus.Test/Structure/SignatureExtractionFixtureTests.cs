using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Minifier;
using Plutus.Domain.Structure;

namespace Plutus.Test.Structure;

/// <summary>
/// Signature extraction over a realistic fixture: what an interface looks like after the pass, that generics with
/// constraints survive, that implementation text is gone, and that running the pass twice changes nothing.
/// </summary>
public class SignatureExtractionFixtureTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Structure", "Service.cs");

    private static string Source => File.ReadAllText(FixturePath);

    [Fact]
    public void InterfaceAndAbstractSurface_IsKeptVerbatim()
    {
        var result = Extract();

        result.Content.Should().Contain("public interface IWidgetService");
        result.Content.Should().Contain("int Count { get; }");
        result.Content.Should().Contain("Widget? Find(string id);");
    }

    [Fact]
    public void DeclarationsSurvive_AndImplementationTextDoesNot()
    {
        var result = Extract();

        // Declarations, attributes, documentation and field initializers survive.
        result.Content.Should().Contain("/// <summary>The default implementation.</summary>");
        result.Content.Should().Contain("private readonly Dictionary<string, Widget> _widgets = new();");
        result.Content.Should().Contain("public WidgetService(IEnumerable<Widget> widgets);");
        result.Content.Should().Contain("public int Count;");
        result.Content.Should().Contain("public Widget? Find(string id);");
        result.Content.Should().Contain("public string Describe<T>(T value)");
        result.Content.Should().Contain("where T : notnull;");
        result.Content.Should().Contain("public string Label");
        result.Content.Should().Contain("get;");

        // No implementation text is left behind.
        result.Content.Should().NotContain("foreach");
        result.Content.Should().NotContain("TryGetValue");
        result.Content.Should().NotContain("return null");
        result.Content.Should().NotContain("_widgets.Count");
        result.Content.Should().NotContain("value.ToString()");
        result.Content.Should().NotContain("{Name} ({Id})");
    }

    [Fact]
    public void EveryImplementationBody_IsCountedByKind()
    {
        var result = Extract();

        result.CountOf(MemberKind.Constructor).Should().Be(1);
        result.CountOf(MemberKind.Method).Should().Be(2);
        result.CountOf(MemberKind.Property).Should().Be(1);
        result.CountOf(MemberKind.Accessor).Should().Be(1);
        result.ParseDiagnostics.Should().Be(0);
    }

    [Fact]
    public void RunningThePassTwice_ChangesNothingTheSecondTime()
    {
        var first = Extract();
        var second = new CSharpSignatureSkeletonProvider().Create(first.Content, SignatureExtractionOptions.Default);

        second.Content.Should().Be(first.Content);
        second.StrippedBodyCount.Should().Be(0);
    }

    [Fact]
    public void CombinedWithMinify_TheSkeletonIsCompact()
    {
        var options = Options(signatures: true, minify: true);
        var minifier = CodeMinifierFactory.Create(options);

        var result = minifier.Minify(Source, LanguageId.CSharp);

        result.Should().Contain("public Widget? Find(string id);");
        result.Should().NotContain("TryGetValue");
        BlankLines(result).Should().Be(0, "minification collapses whitespace-only lines");
    }

    [Fact]
    public void CombinedWithStripComments_TheSkeletonCarriesNoComments()
    {
        var minifier = CodeMinifierFactory.Create(Options(signatures: true, strip: true));

        var result = minifier.Minify(Source, LanguageId.CSharp);

        result.Should().NotContain("/// <summary>");
        result.Should().Contain("public Widget? Find(string id);");
    }

    private static SignatureExtractionResult Extract() =>
        new CSharpSignatureSkeletonProvider().Create(Source, SignatureExtractionOptions.Default);

    /// <summary>Counts whitespace-only lines, ignoring the terminator that ends the text.</summary>
    private static int BlankLines(string text)
    {
        var lines = text.Split('\n');
        var count = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (index == lines.Length - 1 && line.Length == 0)
            {
                continue;
            }

            if (line.Trim().Length == 0)
            {
                count++;
            }
        }

        return count;
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
        TargetEncoding = System.Text.Encoding.UTF8,
        MaxConcurrency = 1,
        Format = "md",
        Branch = null,
        Commit = null,
        Minify = minify,
        StripComments = strip,
        SignaturesOnly = signatures,
    };
}
