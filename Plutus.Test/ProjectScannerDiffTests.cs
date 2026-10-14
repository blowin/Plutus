using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.Diffing;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class ProjectScannerDiffTests
{
    private readonly IFileInfoDetailProvider _detailProvider = Substitute.For<IFileInfoDetailProvider>();
    private readonly IIgnoreMatcher _ignoreMatcher = Substitute.For<IIgnoreMatcher>();
    private readonly IFileProvider _fileProvider = Substitute.For<IFileProvider>();

    [Fact]
    public void Scan_WithDiffPaths_ShouldIncludeOnlyChangedFilesAndDropEmptyDirectories()
    {
        var readme = CreateMockFile("README.md", isDirectory: false);
        var src = CreateMockFile("src", isDirectory: true);
        var docs = CreateMockFile("docs", isDirectory: true);

        SetDirectoryContents("", [readme, src, docs]);
        SetDirectoryContents("src", [CreateMockFile("Core.cs", isDirectory: false), CreateMockFile("Other.cs", isDirectory: false)]);
        SetDirectoryContents("docs", [CreateMockFile("guide.md", isDirectory: false)]);

        var scanner = new ProjectScanner(_detailProvider, _ignoreMatcher);

        var result = scanner.Scan(_fileProvider, "Root", "out.md", FileSize.FromBytes(1024), DiffPathSet.Create(["src/Core.cs"], StringComparer.Ordinal));

        result.Children.Should().HaveCount(3);

        result.Children.Single(c => c.Name == "README.md").Status.Should().Be(NodeStatus.IgnoredFile);
        result.Children.Single(c => c.Name == "docs").Status.Should().Be(NodeStatus.IgnoredDirectory);

        var srcNode = result.Children.Single(c => c.Name == "src");
        srcNode.Status.Should().Be(NodeStatus.Included);
        srcNode.Children.Single(c => c.Name == "Core.cs").Status.Should().Be(NodeStatus.Included);
        srcNode.Children.Single(c => c.Name == "Other.cs").Status.Should().Be(NodeStatus.IgnoredFile);

        result.ExtractBundleEntries().Should().ContainSingle().Which.RelativePath.Should().Be("src/Core.cs");
    }

    [Fact]
    public void Scan_WithEmptyDiffPaths_ShouldExcludeEveryFile()
    {
        var root = CreateMockFile("src", isDirectory: true);
        SetDirectoryContents("", [root]);
        SetDirectoryContents("src", [CreateMockFile("Core.cs", isDirectory: false)]);

        var scanner = new ProjectScanner(_detailProvider, _ignoreMatcher);

        var result = scanner.Scan(_fileProvider, "Root", "out.md", FileSize.FromBytes(1024), DiffPathSet.Empty);

        result.Children.Should().ContainSingle().Which.Status.Should().Be(NodeStatus.IgnoredDirectory);
        result.ExtractBundleEntries().Should().BeEmpty();
    }

    [Fact]
    public void Scan_WithoutDiffPaths_ShouldKeepEveryFileAsBefore()
    {
        SetDirectoryContents("", [CreateMockFile("Core.cs", isDirectory: false), CreateMockFile("Other.cs", isDirectory: false)]);

        var scanner = new ProjectScanner(_detailProvider, _ignoreMatcher);

        var result = scanner.Scan(_fileProvider, "Root", "out.md", FileSize.FromBytes(1024));

        result.Children.Should().AllSatisfy(child => child.Status.Should().Be(NodeStatus.Included));
    }

    [Fact]
    public void Scan_WithDiffPaths_ShouldStillApplyTheIgnoreMatcherToChangedFiles()
    {
        SetDirectoryContents("", [CreateMockFile("secret.env", isDirectory: false)]);
        _ignoreMatcher.IsIgnoredFile("secret.env").Returns(true);

        var scanner = new ProjectScanner(_detailProvider, _ignoreMatcher);

        var result = scanner.Scan(_fileProvider, "Root", "out.md", FileSize.FromBytes(1024), DiffPathSet.Create(["secret.env"], StringComparer.Ordinal));

        result.Children.Single().Status.Should().Be(NodeStatus.IgnoredFile);
    }

    [Fact]
    public void Scan_WithDiffPaths_ShouldNotReadDirectoriesWithoutChanges()
    {
        SetDirectoryContents("", [CreateMockFile("docs", isDirectory: true)]);

        var scanner = new ProjectScanner(_detailProvider, _ignoreMatcher);

        scanner.Scan(_fileProvider, "Root", "out.md", FileSize.FromBytes(1024), DiffPathSet.Create(["src/Core.cs"], StringComparer.Ordinal));

        _fileProvider.DidNotReceive().GetDirectoryContents("docs");
    }

    [Fact]
    public void Scan_WithDiffPaths_WhenChangedFileExceedsMaxSize_ShouldStillMarkItOversized()
    {
        SetDirectoryContents("", [CreateMockFile("Core.cs", isDirectory: false, length: 5000)]);

        var scanner = new ProjectScanner(_detailProvider, _ignoreMatcher);

        var result = scanner.Scan(_fileProvider, "Root", "out.md", FileSize.FromBytes(1000), DiffPathSet.Create(["Core.cs"], StringComparer.Ordinal));

        result.Children.Single().Status.Should().Be(NodeStatus.OversizedFile);
    }

    private void SetDirectoryContents(string subPath, IReadOnlyList<IFileInfo> children)
    {
        var contents = Substitute.For<IDirectoryContents>();
        contents.Exists.Returns(true);
        contents.GetEnumerator().Returns(children.GetEnumerator());
        _fileProvider.GetDirectoryContents(subPath).Returns(contents);
    }

    private static IFileInfo CreateMockFile(string name, bool isDirectory, long length = 0)
    {
        var file = Substitute.For<IFileInfo>();
        file.Name.Returns(name);
        file.IsDirectory.Returns(isDirectory);
        file.Length.Returns(length);
        file.Exists.Returns(true);
        return file;
    }
}
