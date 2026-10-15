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
    public void RenderSection_ShouldNameTheComparisonBaseAndChangedFileCount()
    {
        var diff = CreateContext("abc123", "diff --git a/A.cs b/A.cs", File("src/A.cs"));

        var markdown = BundleDiff.RenderSection(diff, "Bundle scope is limited to");

        markdown.Should().StartWith("## Changes against `abc123`");
        markdown.Should().Contain("Bundle scope is limited to 1 file(s).");
        markdown.Should().Contain("### Raw diff");
        markdown.Should().Contain("```diff");
        markdown.Should().Contain("diff --git a/A.cs b/A.cs");
    }

    [Fact]
    public void RenderSection_WhenNoFilesChanged_ShouldExplainTheEmptyScope()
    {
        var diff = CreateContext("abc123", string.Empty);

        var markdown = BundleDiff.RenderSection(diff, "Bundle scope is limited to");

        markdown.Should().Contain("## Changes against `abc123`");
        markdown.Should().Contain("No changed files were reported for this scope.");
        markdown.Should().NotContain("### Raw diff");
    }

    [Fact]
    public void RenderSection_WhenDiffTextIsWhitespaceOnly_ShouldOmitTheRawDiffBlock()
    {
        var diff = CreateContext("abc123", $"   {Environment.NewLine}  ", File("src/A.cs"));

        var markdown = BundleDiff.RenderSection(diff, "Bundle scope is limited to");

        markdown.Should().Contain("1 file(s).");
        markdown.Should().NotContain("### Raw diff");
    }

    [Fact]
    public void RenderSection_WhenDiffContainsAFence_ShouldUseALongerOuterFence()
    {
        string rawDiff = $"diff --git a/A.cs b/A.cs{Environment.NewLine}+```csharp{Environment.NewLine}";

        var markdown = BundleDiff.RenderSection(CreateContext("abc123", rawDiff, File("A.cs")), "Bundle scope is limited to");

        markdown.Should().Contain("````diff");
        markdown.Should().Contain("````");
        markdown.Should().NotContain($"{Environment.NewLine}```diff");
    }

    [Fact]
    public void RenderSection_WhenDiffDoesNotEndWithNewLine_ShouldStillCloseTheFence()
    {
        string rawDiff = $"diff --git a/A.cs b/A.cs{Environment.NewLine}+last line without newline";

        var markdown = BundleDiff.RenderSection(CreateContext("abc123", rawDiff, File("A.cs")), "Bundle scope is limited to");

        markdown.Should().EndWith($"```{Environment.NewLine}");
        markdown.Should().Contain($"+last line without newline{Environment.NewLine}```");
    }

    [Fact]
    public void RenderSection_WithUntrackedFiles_ShouldListThemAndIncludeTheirGeneratedDiffs()
    {
        var diff = CreateContext("abc123", "diff --git a/A.cs b/A.cs", File("A.cs")) with
        {
            UntrackedFiles = ["src/New.cs"],
            UntrackedRawText = $"diff --git a/src/New.cs b/src/New.cs{Environment.NewLine}new file mode 100644"
        };
        var markdown = BundleDiff.RenderSection(diff, "Bundle scope is limited to");

        markdown.Should().Contain("New files git does not track yet (1), generated diffs below:");
        markdown.Should().Contain("- `src/New.cs`");
        markdown.Should().Contain("### Raw diff");
        markdown.Should().Contain("new file mode 100644");
    }

    [Fact]
    public void RenderSection_WhenOnlyUntrackedFilesChanged_ShouldStillShowTheirDiff()
    {
        var diff = CreateContext("abc123", string.Empty) with
        {
            UntrackedFiles = ["src/New.cs"],
            UntrackedRawText = $"diff --git a/src/New.cs b/src/New.cs{Environment.NewLine}new file mode 100644"
        };

        var markdown = BundleDiff.RenderSection(diff, "Bundle scope is limited to");

        markdown.Should().Contain("- `src/New.cs`");
        markdown.Should().Contain("new file mode 100644");
        markdown.Should().NotContain("No changed files were reported");
    }

    [Fact]
    public void RenderSection_WhenNothingChanged_ShouldExplainTheEmptyScope()
    {
        var markdown = BundleDiff.RenderSection(CreateContext("abc123", string.Empty), "Bundle scope is limited to");

        markdown.Should().Contain("No changed files were reported for this scope.");
        markdown.Should().NotContain("### Raw diff");
    }

    [Fact]
    public void RenderSection_WithoutUntrackedFiles_ShouldNotMentionThem()
    {
        var markdown = BundleDiff.RenderSection(CreateContext("abc123", "diff --git a/A.cs b/A.cs", File("A.cs")), "Bundle scope is limited to");

        markdown.Should().NotContain("git does not track");
    }

    [Fact]
    public void RenderSection_WithNullContext_ShouldThrow()
    {
        var act = () => BundleDiff.RenderSection(null!, "Bundle scope is limited to");

        act.Should().Throw<ArgumentNullException>();
    }

    private static GitDiffContext CreateContext(string comparisonBase, string rawText, params ChangedFile[] files) =>
        new(comparisonBase, files, rawText);

    private static ChangedFile File(string path) => new(path, ChangeKind.Modified);
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
    public async Task WriteAsync_WithDiffContext_ShouldAppendTheRawDiffSection()
    {
        var writer = CreateWriter();
        var diff = CreateContext(
            "abc123",
            $"diff --git a/src/Core.cs b/src/Core.cs{Environment.NewLine}-old{Environment.NewLine}+new",
            new ChangedFile("src/Core.cs", ChangeKind.Modified));

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, FileSize.FromBytes(4096), diff);

        var result = ReadOutput();
        result.Should().Contain("## FILE: `src/Core.cs`");
        result.Should().Contain("## Changes against `abc123`");
        result.Should().Contain("### Raw diff");
        result.Should().Contain("```diff");
        result.Should().Contain("+new");
    }

    [Fact]
    public async Task WriteAsync_WithoutDiffContext_ShouldNotAppendAnyDiffSection()
    {
        var writer = CreateWriter();

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, FileSize.FromBytes(4096));

        ReadOutput().Should().NotContain("## Changes against");
    }

    [Fact]
    public async Task WriteAsync_WithEmptyDiffContext_ShouldReportTheScopeAndKeepTheProvidedFiles()
    {
        // The writer serializes the tree it is handed; restricting the tree is the scanner's responsibility.
        var writer = CreateWriter();
        var diff = CreateContext("abc123", string.Empty);

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, FileSize.FromBytes(4096), diff);

        var result = ReadOutput();
        result.Should().Contain("No changed files were reported for this scope.");
        result.Should().Contain("## FILE: `src/Core.cs`");
    }

    [Fact]
    public async Task WriteAsync_WithFileContentsDisabled_ShouldEmitOnlyTheChangeSummary()
    {
        var writer = CreateWriter();
        writer.IncludeFileContents = false;
        var diff = CreateContext("abc123", $"diff --git a/src/Core.cs b/src/Core.cs{Environment.NewLine}-old{Environment.NewLine}+new", new ChangedFile("src/Core.cs", ChangeKind.Modified));

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, FileSize.FromBytes(4096), diff);

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

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, FileSize.FromBytes(4096));

        var result = ReadOutput();
        result.Should().Contain("## Changed Files");
        result.Should().NotContain("## FILE:");
        result.Should().NotContain("## Changes against");
    }

    [Fact]
    public async Task WriteAsync_WithFileContentsEnabled_ShouldKeepTheFullBundle()
    {
        var writer = CreateWriter();

        await writer.WriteAsync(Roots(Root("Core.cs", "src/Core.cs")), _output, FileSize.FromBytes(4096));

        var result = ReadOutput();
        result.Should().Contain("## File List");
        result.Should().Contain("## Directory Tree");
        result.Should().Contain("## FILE: `src/Core.cs`");
        result.Should().Contain("sample content");
    }

    private MarkdownBundleWriter CreateWriter() =>
        new(_detail, _languageProvider, IdentityCodeMinifier.Instance, Encoding.UTF8, new NoOpTokenBudgetApplier());

    private string ReadOutput() => Encoding.UTF8.GetString(_output.GetResetStream().ToArray());

    private static GitDiffContext CreateContext(string comparisonBase, string rawText, params ChangedFile[] files) =>
        new(comparisonBase, files, rawText);

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
