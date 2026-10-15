using System.Text;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.Diffing;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens.Applier;

namespace Plutus.Test.Diffing;

public class BundleDiffTests
{
    [Fact]
    public void WriteSection_ShouldNameTheComparisonBaseAndFileCount()
    {
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, Context(Chunk("src/A.cs")), "Bundle scope is limited to", includeFileContents: true);

        var markdown = writer.ToString();
        markdown.Should().StartWith("## Changes against `abc123`");
        markdown.Should().Contain("Bundle scope is limited to 1 file(s).");
    }

    [Fact]
    public void WriteSection_ShouldFenceEveryFileSeparately()
    {
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, Context(Chunk("src/A.cs"), Chunk("src/B.cs")), "Bundle scope is limited to", includeFileContents: true);

        var markdown = writer.ToString();
        markdown.Should().Contain("### DIFF: `src/A.cs`");
        markdown.Should().Contain("### DIFF: `src/B.cs`");
        markdown.Should().Contain("```diff");
        markdown.Should().Contain("- Change: `modified`; lines: `+2/-1`; tokens: `~7`");
    }

    [Fact]
    public void WriteSection_WhenAChunkWasOmitted_ShouldReportTheReasonInPlace()
    {
        var omitted = Chunk("src/Huge.cs") with { SkipReason = "omitted by token budget (10 tokens; strategy: PathOrder)" };
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, Context(Chunk("src/A.cs"), omitted), "Bundle scope is limited to", includeFileContents: true);

        var markdown = writer.ToString();
        markdown.Should().Contain("### DIFF: `src/Huge.cs`");
        markdown.Should().Contain("Diff omitted: omitted by token budget (10 tokens; strategy: PathOrder)");
        markdown.Should().NotContain("- Change: `modified`; lines: `+2/-1`; tokens: `~7`\n\n```diff\ndiff --git a/src/Huge.cs");
    }

    [Fact]
    public void WriteSection_WithUntrackedChunks_ShouldAnnounceThemSeparately()
    {
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, ContextWithUntracked([Chunk("src/A.cs")], [Chunk("src/New.cs")]), "Bundle scope is limited to", includeFileContents: true);

        var markdown = writer.ToString();
        markdown.Should().Contain("New files git does not track yet (1), generated diffs included below:");
        markdown.Should().Contain("### DIFF: `src/New.cs`");
    }

    [Fact]
    public void WriteSection_WhenNoFilesChanged_ShouldExplainTheEmptyScope()
    {
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, Context(), "Bundle scope is limited to", includeFileContents: true);

        var markdown = writer.ToString();
        markdown.Should().Contain("No changed files were reported for this scope.");
        markdown.Should().NotContain("### DIFF:");
    }

    [Fact]
    public void WriteSection_WhenContentsAreIncludedAndNothingChanged_ShouldNotRepeatTheFileCount()
    {
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, Context(), "Bundle scope is limited to", includeFileContents: true);

        writer.ToString().Should().NotContain("Bundle scope is limited to");
    }

    [Fact]
    public void WriteSection_WhenContentsAreOmitted_ShouldAlwaysStateTheScope()
    {
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, Context(), "Bundle scope is limited to", includeFileContents: false);

        writer.ToString().Should().Contain("Bundle scope is limited to 0 file(s).");
    }

    [Fact]
    public void WriteSection_WhenDiffContainsAFence_ShouldUseALongerOuterFence()
    {
        var chunk = new DiffChunk("src/A.cs", "src/A.cs", ChangeKind.Modified, "diff --git a/src/A.cs b/src/A.cs\n+```csharp\n", 1, 0);
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, Context(chunk), "Bundle scope is limited to", includeFileContents: true);

        writer.ToString().Should().Contain("````diff");
    }

    [Fact]
    public void WriteSection_WhenDiffDoesNotEndWithNewLine_ShouldStillCloseTheFence()
    {
        var chunk = new DiffChunk("src/A.cs", "src/A.cs", ChangeKind.Modified, "diff --git a/src/A.cs b/src/A.cs\n+last", 1, 0);
        var writer = CreateWriter();

        BundleDiff.WriteSection(writer, Context(chunk), "Bundle scope is limited to", includeFileContents: true);

        writer.ToString().Should().Contain("+last\n```");
    }

    [Fact]
    public void WriteSection_WithNullContext_ShouldThrow()
    {
        var act = () => BundleDiff.WriteSection(CreateWriter(), null!, "Bundle scope is limited to", includeFileContents: true);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>Matches the production writer, which uses an explicitly encoded StreamWriter and writes "\n".</summary>
    private static StringWriter CreateWriter() => new() { NewLine = "\n" };

    private static GitDiffContext Context(params DiffChunk[] tracked) => ContextWithUntracked(tracked, []);

    private static GitDiffContext ContextWithUntracked(DiffChunk[] tracked, DiffChunk[] untracked) =>
        new GitDiffContext("abc123")
        {
            FileGroups =
            [
                new DiffFileGroup("/repo", [.. tracked.Select(WithTokens)], [.. untracked.Select(WithTokens)])
            ]
        };

    private static DiffChunk WithTokens(DiffChunk chunk) => chunk with { EstimatedTokens = 7 };

    private static DiffChunk Chunk(string path) =>
        new(path, path, ChangeKind.Modified, $"diff --git a/{path} b/{path}\n-old\n+new\n+added", 2, 1);
}

public class MarkdownBundleWriterDiffTests
{
    private readonly IFileInfoDetailProvider _detail = Substitute.For<IFileInfoDetailProvider>();
    private readonly IMarkdownLanguageProvider _languageProvider = Substitute.For<IMarkdownLanguageProvider>();
    private readonly TestFileInfo _output = new() { PhysicalPath = "out.md", Name = "out.md" };

    public MarkdownBundleWriterDiffTests()
    {
        _detail.CreateWriterForFile(_output, Arg.Any<Encoding?>())
            .Returns(_ => new StreamWriter(_output.Stream, leaveOpen: true));
        _languageProvider.GetLanguage(Arg.Any<string>()).Returns("text");
    }

    [Fact]
    public async Task WriteAsync_WithDiffContext_ShouldAppendTheDiffSection()
    {
        var writer = CreateWriter();

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, DiffContext());

        var result = ReadOutput();
        result.Should().Contain("## FILE: `src/Core.cs`");
        result.Should().Contain("## Changes against `abc123`");
        result.Should().Contain("### DIFF: `src/Core.cs`");
        result.Should().Contain("+new");
    }

    [Fact]
    public async Task WriteAsync_WithoutDiffContext_ShouldNotAppendAnyDiffSection()
    {
        var writer = CreateWriter();

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output);

        ReadOutput().Should().NotContain("## Changes against");
    }

    [Fact]
    public async Task WriteAsync_WithEmptyDiffContext_ShouldReportTheScopeAndKeepTheProvidedFiles()
    {
        // The writer serializes the tree it is handed; restricting the tree is the scanner's responsibility.
        var writer = CreateWriter();

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, EmptyDiffContext());

        var result = ReadOutput();
        result.Should().Contain("No changed files were reported for this scope.");
        result.Should().Contain("## FILE: `src/Core.cs`");
    }

    [Fact]
    public async Task WriteAsync_WithFileContentsDisabled_ShouldEmitOnlyTheChangeSummary()
    {
        var writer = CreateWriter();
        writer.IncludeFileContents = false;

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, DiffContext());

        var result = ReadOutput();
        result.Should().Contain("# Project Bundle");
        result.Should().Contain("## Changed Files");
        result.Should().Contain("src/Core.cs");
        result.Should().Contain("## Changes against `abc123`");
        result.Should().Contain("+new");

        // The change itself is the artifact: no source, no directory tree, no per-file blocks.
        result.Should().NotContain("## FILE:");
        result.Should().NotContain("## Directory Tree");
        result.Should().NotContain("sample content");
    }

    [Fact]
    public async Task WriteAsync_WithFileContentsDisabledAndNoDiff_ShouldNotClaimToBeADiffBundle()
    {
        var writer = CreateWriter();
        writer.IncludeFileContents = false;

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output);

        var result = ReadOutput();
        result.Should().Contain("## Changed Files");
        result.Should().NotContain("## FILE:");
        result.Should().NotContain("## Changes against");
    }

    [Fact]
    public async Task WriteAsync_WithFileContentsEnabled_ShouldKeepTheFullBundle()
    {
        var writer = CreateWriter();

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output);

        var result = ReadOutput();
        result.Should().Contain("## File List");
        result.Should().Contain("## Directory Tree");
        result.Should().Contain("## FILE: `src/Core.cs`");
        result.Should().Contain("sample content");
    }

    private MarkdownBundleWriter CreateWriter() =>
        new(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null));

    private string ReadOutput() => Encoding.UTF8.GetString(_output.GetResetStream().ToArray());

    private static GitDiffContext DiffContext() =>
        new("abc123")
        {
            FileGroups =
            [
                new DiffFileGroup(
                    "Root",
                    [new DiffChunk("src/Core.cs", "src/Core.cs", ChangeKind.Modified, "diff --git a/src/Core.cs b/src/Core.cs\n-old\n+new", 1, 1) { EstimatedTokens = 7 }],
                    [])
            ]
        };

    private static GitDiffContext EmptyDiffContext() =>
        new("abc123") { FileGroups = [new DiffFileGroup("Root", [], [])] };

    private static List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> Roots(
        (IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode) root) => [root];

    private static (IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode) Root(string name, string relativePath)
    {
        var file = Substitute.For<IFileInfo>();
        file.Name.Returns(name);
        file.Exists.Returns(true);
        file.Length.Returns(12);
        file.CreateReadStream().Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes("sample content")));

        return (new NullFileProvider(), "Root", "Root", new ProjectNode
        {
            Name = name,
            RelativePath = relativePath,
            IsDirectory = false,
            Status = NodeStatus.Included,
            FileInfo = file
        });
    }
}
