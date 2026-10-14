using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
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
    public async Task JsonWriter_WithUntrackedFiles_ShouldListThemWithTheirGeneratedDiff()
    {
        var writer = new JsonBundleWriter(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier());
        var context = CreateContext() with
        {
            UntrackedFiles = ["src/New.cs"],
            UntrackedRawText = "diff --git a/src/New.cs b/src/New.cs\nnew file mode 100644"
        };

        await writer.WriteAsync(Roots(), _output, FileSize.FromBytes(4096), context);

        using var document = JsonDocument.Parse(File.ReadAllText(OutputPath));
        var diff = document.RootElement.GetProperty("Diff");

        diff.GetProperty("UntrackedFiles").EnumerateArray().Select(item => item.GetString()).Should().Equal("src/New.cs");
        diff.GetProperty("UntrackedDiff").GetString().Should().Contain("new file mode 100644");
        diff.GetProperty("RawDiff").GetString().Should().Be(RawDiff);
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
    public async Task XmlWriter_WithUntrackedFiles_ShouldEmitThemWithTheirGeneratedDiff()
    {
        var writer = new XmlBundleWriter(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier());
        var context = CreateContext() with
        {
            UntrackedFiles = ["src/New.cs"],
            UntrackedRawText = "diff --git a/src/New.cs b/src/New.cs\nnew file mode 100644"
        };

        await writer.WriteAsync(Roots(), _output, FileSize.FromBytes(4096), context);

        var diff = XDocument.Load(OutputPath).Root!.Element("Diff")!;

        diff.Elements("UntrackedFile").Should().ContainSingle()
            .Which.Attribute("path")!.Value.Should().Be("src/New.cs");
        diff.Element("UntrackedDiff")!.Value.Should().Contain("new file mode 100644");
        diff.Element("RawDiff")!.Value.Should().Be(RawDiff);
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

    [Fact]
    public async Task JsonWriter_WithFileContentsDisabled_ShouldOmitContentButKeepTheDiff()
    {
        var writer = new JsonBundleWriter(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier())
        {
            IncludeFileContents = false
        };

        await writer.WriteAsync(Roots(), _output, FileSize.FromBytes(4096), CreateContext());

        using var document = JsonDocument.Parse(File.ReadAllText(OutputPath));
        var file = document.RootElement.GetProperty("Files").EnumerateArray().Single();

        file.TryGetProperty("Content", out _).Should().BeFalse();
        document.RootElement.GetProperty("Diff").GetProperty("RawDiff").GetString().Should().Be(RawDiff);
    }

    [Fact]
    public async Task XmlWriter_WithFileContentsDisabled_ShouldOmitContentButKeepTheDiff()
    {
        var writer = new XmlBundleWriter(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier())
        {
            IncludeFileContents = false
        };

        await writer.WriteAsync(Roots(), _output, FileSize.FromBytes(4096), CreateContext());

        var document = XDocument.Load(OutputPath);
        document.Root!.Element("Files")!.Elements("File").Single().Element("Content").Should().BeNull();
        document.Root!.Element("Diff")!.Element("RawDiff")!.Value.Should().Be(RawDiff);
    }

    private static GitDiffContext CreateContext(string rawDiff = RawDiff) =>
        new("basesha", [new ChangedFile("src/Core.cs", ChangeKind.Added)], rawDiff);

    private List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> Roots()
    {
        var file = Substitute.For<IFileInfo>();
        file.Name.Returns("Core.cs");
        file.Exists.Returns(true);
        file.Length.Returns(12);
        file.CreateReadStream().Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes("sample content")));

        var root = new ProjectNode
        {
            Name = "Root/",
            RelativePath = string.Empty,
            IsDirectory = true,
            Status = NodeStatus.Included
        };
        root.Children.Add(new ProjectNode
        {
            Name = "Core.cs",
            RelativePath = "src/Core.cs",
            IsDirectory = false,
            Status = NodeStatus.Included,
            FileInfo = file
        });

        return [(new NullFileProvider(), "Root", "/repo", root)];
    }
}
