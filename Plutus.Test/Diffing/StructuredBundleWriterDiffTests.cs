using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.Diffing;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens.Applier;
using Plutus.Infrastructure;

namespace Plutus.Test.Diffing;

/// <summary>
/// Covers the structured diff section, which writes to a real file because the JSON and XML writers accept
/// any <see cref="IFileInfo"/>. Each test removes the file it created.
/// </summary>
public class StructuredBundleWriterDiffTests : IDisposable
{
    private const string RawDiff = "diff --git a/src/Core.cs b/src/Core.cs\n@@ -1 +1 @@\n-old\n+new";

    private static readonly string OutputPath = Path.Combine(Path.GetTempPath(), $"plutus-diff-{Guid.NewGuid():N}.out");

    private readonly IFileInfoDetailProvider _detail = new PhysicianPlutusFileInfo();
    private readonly IMarkdownLanguageProvider _languageProvider = new MarkdownLanguageProvider();
    private readonly IFileInfo _output;

    public StructuredBundleWriterDiffTests()
    {
        _output = _detail.CreateFile(OutputPath);
    }

    public void Dispose()
    {
        if (File.Exists(OutputPath))
        {
            File.Delete(OutputPath);
        }
    }

    [Fact]
    public async Task JsonWriter_WithDiffContext_ShouldEmitMetadataAndChangedFiles()
    {
        var writer = new JsonBundleWriter(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier());

        await writer.WriteAsync(Roots(), _output, FileSize.FromBytes(4096), CreateContext());

        using var document = JsonDocument.Parse(File.ReadAllText(OutputPath));
        var root = document.RootElement;

        root.GetProperty("Metadata").GetProperty("DiffScoped").GetBoolean().Should().BeTrue();
        root.GetProperty("Metadata").GetProperty("DiffComparisonBase").GetString().Should().Be("basesha");

        var diff = root.GetProperty("Diff");
        diff.GetProperty("ComparisonBase").GetString().Should().Be("basesha");
        diff.GetProperty("RawDiff").GetString().Should().Be(RawDiff);

        var changed = diff.GetProperty("Files").EnumerateArray().Should().ContainSingle().Subject;
        changed.GetProperty("RelativePath").GetString().Should().Be("src/Core.cs");
        changed.GetProperty("ChangeKind").GetString().Should().Be("added");
    }

    [Fact]
    public async Task JsonWriter_WithoutDiffContext_ShouldOmitTheDiffSection()
    {
        var writer = new JsonBundleWriter(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier());

        await writer.WriteAsync(Roots(), _output, FileSize.FromBytes(4096));

        using var document = JsonDocument.Parse(File.ReadAllText(OutputPath));
        var root = document.RootElement;

        root.GetProperty("Metadata").GetProperty("DiffScoped").GetBoolean().Should().BeFalse();
        root.TryGetProperty("Diff", out _).Should().BeFalse();
    }

    [Fact]
    public async Task XmlWriter_WithDiffContext_ShouldEmitMetadataAndEscapedRawDiff()
    {
        var writer = new XmlBundleWriter(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier());

        await writer.WriteAsync(Roots(), _output, FileSize.FromBytes(4096), CreateContext());

        var document = XDocument.Load(OutputPath);
        var metadata = document.Root!.Element("Metadata")!;

        metadata.Element("DiffScoped")!.Value.Should().Be("true");
        metadata.Element("DiffComparisonBase")!.Value.Should().Be("basesha");

        var diff = document.Root!.Element("Diff")!;
        diff.Attribute("comparisonBase")!.Value.Should().Be("basesha");
        diff.Element("RawDiff")!.Value.Should().Be(RawDiff);

        var changed = diff.Elements("ChangedFile").Should().ContainSingle().Subject;
        changed.Attribute("path")!.Value.Should().Be("src/Core.cs");
        changed.Attribute("change")!.Value.Should().Be("added");
    }

    [Fact]
    public async Task XmlWriter_WhenRawDiffContainsCdataTerminator_ShouldStayWellFormed()
    {
        const string hostileDiff = "diff --git a/A.cs b/A.cs\n+content with ]]> inside\n";
        var writer = new XmlBundleWriter(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier());

        await writer.WriteAsync(Roots(), _output, FileSize.FromBytes(4096), CreateContext(hostileDiff));

        var document = XDocument.Load(OutputPath);
        document.Root!.Element("Diff")!.Element("RawDiff")!.Value.Should().Be(hostileDiff);
    }

    private static GitDiffContext CreateContext(string rawDiff = RawDiff) =>
        new("basesha", [new ChangedFile("src/Core.cs", ChangeKind.Added)], rawDiff);

    private List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> Roots() =>
    [
        (new NullFileProvider(), "Root", "/repo", new ProjectNode
        {
            Name = "Root/",
            RelativePath = string.Empty,
            IsDirectory = true,
            Status = NodeStatus.Included
        })
    ];
}
