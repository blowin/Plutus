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

namespace Plutus.Test.Diffing;

public class StructuredBundleWriterDiffTests
{
    private const string PatchText = "diff --git a/src/Core.cs b/src/Core.cs\n@@ -1 +1 @@\n-old\n+new";

    private readonly IFileInfoDetailProvider _detail = Substitute.For<IFileInfoDetailProvider>();
    private readonly IMarkdownLanguageProvider _languageProvider = Substitute.For<IMarkdownLanguageProvider>();
    private readonly TestFileInfo _output = new() { PhysicalPath = "out.bundle", Name = "out.bundle" };

    public StructuredBundleWriterDiffTests()
    {
        _detail.CreateWriterForFile(_output, Arg.Any<Encoding?>())
            .Returns(_ => new StreamWriter(_output.Stream, leaveOpen: true));
    }

    [Fact]
    public async Task JsonWriter_WithDiffContext_ShouldEmitMetadataAndPerFileChunks()
    {
        var writer = new JsonBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

        await writer.WriteAsync(Roots(), _output, CreateContext());

        var root = ParseJson().RootElement;

        root.GetProperty("Metadata").GetProperty("DiffScoped").GetBoolean().Should().BeTrue();
        root.GetProperty("Metadata").GetProperty("DiffComparisonBase").GetString().Should().Be("basesha");

        var diff = root.GetProperty("Diff");
        diff.GetProperty("ComparisonBase").GetString().Should().Be("basesha");

        var chunk = diff.GetProperty("Chunks").EnumerateArray().Should().ContainSingle().Subject;
        chunk.GetProperty("RelativePath").GetString().Should().Be("src/Core.cs");
        chunk.GetProperty("ChangeKind").GetString().Should().Be("added");
        chunk.GetProperty("AddedLines").GetInt32().Should().Be(1);
        chunk.GetProperty("DeletedLines").GetInt32().Should().Be(1);
        chunk.GetProperty("Diff").GetString().Should().Be(PatchText);
    }

    [Fact]
    public async Task JsonWriter_WhenAChunkWasOmitted_ShouldOmitItsPatchAndReportWhy()
    {
        var omitted = Chunk() with { SkipReason = "omitted by token budget (10 tokens; strategy: PathOrder)" };
        var writer = new JsonBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

        await writer.WriteAsync(Roots(), _output, CreateContext(omitted));

        var chunk = ParseJson().RootElement.GetProperty("Diff").GetProperty("Chunks").EnumerateArray().Single();

        chunk.GetProperty("IsSkipped").GetBoolean().Should().BeTrue();
        chunk.GetProperty("SkipReason").GetString().Should().Contain("omitted by token budget");
        chunk.TryGetProperty("Diff", out _).Should().BeFalse();
    }

    [Fact]
    public async Task JsonWriter_WithUntrackedChunks_ShouldKeepThemApartFromTrackedChunks()
    {
        var writer = new JsonBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

        await writer.WriteAsync(Roots(), _output, CreateContext(untracked: [Chunk("src/New.cs")]));

        var diff = ParseJson().RootElement.GetProperty("Diff");

        diff.GetProperty("Chunks").EnumerateArray().Should().ContainSingle();
        diff.GetProperty("UntrackedFiles").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("RelativePath").GetString().Should().Be("src/New.cs");
    }

    [Fact]
    public async Task JsonWriter_WithoutDiffContext_ShouldOmitTheDiffSection()
    {
        var writer = new JsonBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

        await writer.WriteAsync(Roots(), _output);

        var root = ParseJson().RootElement;

        root.GetProperty("Metadata").GetProperty("DiffScoped").GetBoolean().Should().BeFalse();
        root.TryGetProperty("Diff", out _).Should().BeFalse();
    }

    [Fact]
    public async Task JsonWriter_WithFileContentsDisabled_ShouldOmitContentButKeepTheDiff()
    {
        var writer = new JsonBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null))
        {
            IncludeFileContents = false
        };

        await writer.WriteAsync(Roots(), _output, CreateContext());

        var root = ParseJson().RootElement;
        root.GetProperty("Files").EnumerateArray().Single().TryGetProperty("Content", out _).Should().BeFalse();
        root.GetProperty("Diff").GetProperty("Chunks").EnumerateArray().Should().ContainSingle();
    }

    [Fact]
    public async Task XmlWriter_WithDiffContext_ShouldEmitMetadataAndPerFileChunks()
    {
        var writer = new XmlBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

        await writer.WriteAsync(Roots(), _output, CreateContext());

        var document = ParseXml();
        var metadata = document.Root!.Element("Metadata")!;

        metadata.Element("DiffScoped")!.Value.Should().Be("true");
        metadata.Element("DiffComparisonBase")!.Value.Should().Be("basesha");

        var diff = document.Root!.Element("Diff")!;
        diff.Attribute("comparisonBase")!.Value.Should().Be("basesha");

        var chunk = diff.Elements("Chunk").Should().ContainSingle().Subject;
        chunk.Attribute("path")!.Value.Should().Be("src/Core.cs");
        chunk.Attribute("change")!.Value.Should().Be("added");
        chunk.Attribute("addedLines")!.Value.Should().Be("1");
        chunk.Element("Patch")!.Value.Should().Be(PatchText);
    }

    [Fact]
    public async Task XmlWriter_WhenAChunkWasOmitted_ShouldReportTheReasonInsteadOfAPatch()
    {
        var omitted = Chunk() with { SkipReason = "diff is larger than max-file-size (2.0 MB > 1.0 MB)" };
        var writer = new XmlBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

        await writer.WriteAsync(Roots(), _output, CreateContext(omitted));

        var chunk = ParseXml().Root!.Element("Diff")!.Elements("Chunk").Single();

        chunk.Attribute("skipped")!.Value.Should().Be("true");
        chunk.Attribute("reason")!.Value.Should().Contain("max-file-size");
        chunk.Element("Patch").Should().BeNull();
    }

    [Fact]
    public async Task XmlWriter_WithUntrackedChunks_ShouldKeepThemApartFromTrackedChunks()
    {
        var writer = new XmlBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

        await writer.WriteAsync(Roots(), _output, CreateContext(untracked: [Chunk("src/New.cs")]));

        var diff = ParseXml().Root!.Element("Diff")!;

        diff.Elements("Chunk").Should().ContainSingle();
        diff.Elements("UntrackedChunk").Should().ContainSingle()
            .Which.Attribute("path")!.Value.Should().Be("src/New.cs");
    }

    [Fact]
    public async Task XmlWriter_WhenPatchContainsCdataTerminator_ShouldStayWellFormed()
    {
        var hostile = Chunk() with { Text = "diff --git a/A.cs b/A.cs\n+content with ]]> inside" };
        var writer = new XmlBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

        await writer.WriteAsync(Roots(), _output, CreateContext(hostile));

        ParseXml().Root!.Element("Diff")!.Elements("Chunk").Single().Element("Patch")!.Value.Should().Be(hostile.Text);
    }

    [Fact]
    public async Task XmlWriter_WithFileContentsDisabled_ShouldOmitContentButKeepTheDiff()
    {
        var writer = new XmlBundleWriter(_detail, _languageProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null))
        {
            IncludeFileContents = false
        };

        await writer.WriteAsync(Roots(), _output, CreateContext());

        var document = ParseXml();
        document.Root!.Element("Files")!.Elements("File").Single().Element("Content").Should().BeNull();
        document.Root!.Element("Diff")!.Elements("Chunk").Should().ContainSingle();
    }

    private JsonDocument ParseJson() => JsonDocument.Parse(ReadOutput());

    private XDocument ParseXml() => XDocument.Parse(ReadOutput());

    private string ReadOutput() => Encoding.UTF8.GetString(_output.GetResetStream().ToArray());

    private static GitDiffContext CreateContext(DiffChunk? tracked = null, DiffChunk[]? untracked = null) =>
        new("basesha")
        {
            FileGroups = [new DiffFileGroup("Root", [tracked ?? Chunk()], untracked ?? [])]
        };

    private static DiffChunk Chunk(string path = "src/Core.cs") =>
        new(path, path, ChangeKind.Added, PatchText, 1, 1) { EstimatedTokens = 5 };

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
