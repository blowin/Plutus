using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class ProjectScannerTests
{
    private readonly IFileInfoDetailProvider _mockDetailProvider = Substitute.For<IFileInfoDetailProvider>();
    private readonly IIgnoreMatcher _mockMatcher = Substitute.For<IIgnoreMatcher>();
    private readonly IFileProvider _mockProvider = Substitute.For<IFileProvider>();

    [Fact]
    public void Scan_WithValidFilesAndDirectories_ShouldBuildCorrectTreeStructure()
    {
        // Arrange
        var rootContents = Substitute.For<IDirectoryContents>();
        var subDir = CreateMockFile("src", isDirectory: true);
        var fileReadme = CreateMockFile("README.md", isDirectory: false, length: 500);
        
        rootContents.Exists.Returns(true);
        rootContents.GetEnumerator().Returns(new List<IFileInfo> { subDir, fileReadme }.GetEnumerator());
        _mockProvider.GetDirectoryContents("").Returns(rootContents);

        var srcContents = Substitute.For<IDirectoryContents>();
        var fileApp = CreateMockFile("App.cs", isDirectory: false, length: 1500);
        
        srcContents.Exists.Returns(true);
        srcContents.GetEnumerator().Returns(new List<IFileInfo> { fileApp }.GetEnumerator());
        _mockProvider.GetDirectoryContents("src").Returns(srcContents);

        var scanner = new ProjectScanner(_mockDetailProvider, _mockMatcher);

        // Act
        var result = scanner.Scan(_mockProvider, "Root", "out.md", maxFileSize: 2048);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Root/");
        result.Status.Should().Be(NodeStatus.Included);
        result.Children.Should().HaveCount(2);

        var srcNode = result.Children.First(c => c.Name == "src");
        srcNode.IsDirectory.Should().BeTrue();
        srcNode.Status.Should().Be(NodeStatus.Included);
        srcNode.Children.Should().ContainSingle(c => c.Name == "App.cs" && c.Status == NodeStatus.Included);

        var readmeNode = result.Children.First(c => c.Name == "README.md");
        readmeNode.IsDirectory.Should().BeFalse();
        readmeNode.Status.Should().Be(NodeStatus.Included);
    }

    [Fact]
    public void Scan_WhenFileExceedsMaxFileSize_ShouldMarkAsOversized()
    {
        // Arrange
        var rootContents = Substitute.For<IDirectoryContents>();
        var largeFile = CreateMockFile("LargeVideo.mp4", isDirectory: false, length: 5000);
        
        rootContents.Exists.Returns(true);
        rootContents.GetEnumerator().Returns(new List<IFileInfo> { largeFile }.GetEnumerator());
        _mockProvider.GetDirectoryContents("").Returns(rootContents);

        var scanner = new ProjectScanner(_mockDetailProvider, _mockMatcher);

        // Act
        var result = scanner.Scan(_mockProvider, "Root", "out.md", maxFileSize: 1000);

        // Assert
        var fileNode = result.Children.Should().ContainSingle().Subject;
        fileNode.Name.Should().Be("LargeVideo.mp4");
        fileNode.Status.Should().Be(NodeStatus.OversizedFile);
    }

    [Fact]
    public void Scan_WhenDirectoryIsIgnored_ShouldMarkAsIgnoredAndNotGoDeeper()
    {
        // Arrange
        var rootContents = Substitute.For<IDirectoryContents>();
        var binDir = CreateMockFile("bin", isDirectory: true);
        
        rootContents.Exists.Returns(true);
        rootContents.GetEnumerator().Returns(new List<IFileInfo> { binDir }.GetEnumerator());
        _mockProvider.GetDirectoryContents("").Returns(rootContents);

        _mockMatcher.IsIgnoredDirectory("bin").Returns(true);

        var scanner = new ProjectScanner(_mockDetailProvider, _mockMatcher);

        // Act
        var result = scanner.Scan(_mockProvider, "Root", "out.md", maxFileSize: 1024);

        // Assert
        var dirNode = result.Children.Should().ContainSingle().Subject;
        dirNode.Name.Should().Be("bin");
        dirNode.Status.Should().Be(NodeStatus.IgnoredDirectory);
        dirNode.Children.Should().BeEmpty();
        
        // Проверяем, что сканер не запрашивал внутренности игнорируемой папки
        _mockProvider.DidNotReceive().GetDirectoryContents("bin");
    }

    [Fact]
    public void Scan_WhenFileIsIgnored_ShouldMarkAsIgnoredFile()
    {
        // Arrange
        var rootContents = Substitute.For<IDirectoryContents>();
        var ignoredFile = CreateMockFile(".env", isDirectory: false, length: 100);
        
        rootContents.Exists.Returns(true);
        rootContents.GetEnumerator().Returns(new List<IFileInfo> { ignoredFile }.GetEnumerator());
        _mockProvider.GetDirectoryContents("").Returns(rootContents);

        _mockMatcher.IsIgnoredFile(".env").Returns(true);

        var scanner = new ProjectScanner(_mockDetailProvider, _mockMatcher);

        // Act
        var result = scanner.Scan(_mockProvider, "Root", "out.md", maxFileSize: 1024);

        // Assert
        var fileNode = result.Children.Should().ContainSingle().Subject;
        fileNode.Name.Should().Be(".env");
        fileNode.Status.Should().Be(NodeStatus.IgnoredFile);
    }

    [Fact]
    public void Scan_WhenItemIsReparsePoint_ShouldSkipItEntirely()
    {
        // Arrange
        var rootContents = Substitute.For<IDirectoryContents>();
        var symlinkNode = CreateMockFile("symlink_dir", isDirectory: true, physicalPath: @"C:\symlink");
        
        rootContents.Exists.Returns(true);
        rootContents.GetEnumerator().Returns(new List<IFileInfo> { symlinkNode }.GetEnumerator());
        _mockProvider.GetDirectoryContents("").Returns(rootContents);

        _mockDetailProvider.IsReparsePoint(symlinkNode).Returns(true);

        var scanner = new ProjectScanner(_mockDetailProvider, _mockMatcher);

        // Act
        var result = scanner.Scan(_mockProvider, "Root", "out.md", maxFileSize: 1024);

        // Assert
        result.Children.Should().BeEmpty();
    }

    [Fact]
    public void Scan_WhenFileIsSameAsOutputPhysicalPath_ShouldSkipItEntirely()
    {
        // Arrange
        var rootContents = Substitute.For<IDirectoryContents>();
        var outputPath = @"C:\Project\bundle.md";
        var outputFile = CreateMockFile("bundle.md", isDirectory: false, physicalPath: outputPath);
        
        rootContents.Exists.Returns(true);
        rootContents.GetEnumerator().Returns(new List<IFileInfo> { outputFile }.GetEnumerator());
        _mockProvider.GetDirectoryContents("").Returns(rootContents);

        var scanner = new ProjectScanner(_mockDetailProvider, _mockMatcher);

        // Act
        var result = scanner.Scan(_mockProvider, "Root", outputPath, maxFileSize: 1024);

        // Assert
        result.Children.Should().BeEmpty();
    }

    private static IFileInfo CreateMockFile(string name, bool isDirectory, long length = 0, string? physicalPath = null)
    {
        var file = Substitute.For<IFileInfo>();
        file.Name.Returns(name);
        file.IsDirectory.Returns(isDirectory);
        file.Length.Returns(length);
        file.PhysicalPath.Returns(physicalPath);
        file.Exists.Returns(true);
        return file;
    }
}
