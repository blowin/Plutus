using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Infrastructure;

namespace Plutus.Test;

public class ProjectContextResolverTests
{
    private readonly IFileInfoDetailProvider _mockProvider = Substitute.For<IFileInfoDetailProvider>();
    private readonly IRemoteRepository _mockRepo = Substitute.For<IRemoteRepository>();

    [Fact]
    public async Task ResolveContextAsync_WithEmptyPaths_ShouldReturnNull()
    {
        // Arrange
        var resolver = new ProjectContextResolver(_mockProvider, [_mockRepo]);

        // Act
        var result = await resolver.ResolveContextAsync([], "out.md");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveContextAsync_WhenOutputIsDirectory_ShouldReturnNull()
    {
        // Arrange
        var mockDir = Substitute.For<IFileInfo>();
        mockDir.Exists.Returns(true);
        mockDir.IsDirectory.Returns(true); // Выходной путь ведет на папку

        _mockProvider.CreateDirectory(Arg.Any<string>()).Returns(mockDir);
        _mockProvider.CreateFile(Arg.Any<string>()).Returns(mockDir);

        var resolver = new ProjectContextResolver(_mockProvider, [_mockRepo]);

        // Act
        var result = await resolver.ResolveContextAsync(["C:\\Src"], "C:\\OutDir");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveContextAsync_WithValidLocalPathAndNoOutput_ShouldGenerateDefaultOutput()
    {
        // Arrange
        var localDir = Substitute.For<IFileInfo>();
        localDir.Exists.Returns(true);
        localDir.IsDirectory.Returns(true);
        localDir.Name.Returns("MyProject");

        var defaultOutput = Substitute.For<IFileInfo>();
        defaultOutput.IsDirectory.Returns(false);

        _mockProvider.CreateDirectory(Arg.Any<string>()).Returns(localDir);
        _mockProvider.CreateFile(Arg.Any<string>()).Returns(defaultOutput);

        var resolver = new ProjectContextResolver(_mockProvider, [_mockRepo]);

        // Act
        var result = await resolver.ResolveContextAsync(["MyProject"], null);

        // Assert
        result.Should().NotBeNull();
        result!.Output.Should().Be(defaultOutput);
        result.Roots.Should().ContainSingle().Which.Should().Be(localDir);
    }

    [Fact]
    public async Task ResolveContextAsync_WithRemoteRepository_ShouldInvokeDownload()
    {
        // Arrange
        var remoteUrl = "https://github.com";
        _mockRepo.IsSupportedPath(remoteUrl).Returns(true);
        _mockRepo.DownloadAsync(remoteUrl).Returns(ValueTask.FromResult("C:\\Temp\\DownloadedRepo"));

        var remoteDir = Substitute.For<IFileInfo>();
        remoteDir.Exists.Returns(true);
        remoteDir.IsDirectory.Returns(true);
        remoteDir.Name.Returns("repo");

        var output = Substitute.For<IFileInfo>();
        output.IsDirectory.Returns(false);

        _mockProvider.CreateDirectory(Arg.Any<string>()).Returns(remoteDir);
        _mockProvider.CreateFile(Arg.Any<string>()).Returns(output);

        var resolver = new ProjectContextResolver(_mockProvider, [_mockRepo]);

        // Act
        var result = await resolver.ResolveContextAsync([remoteUrl], "out.md");

        // Assert
        result.Should().NotBeNull();
        await _mockRepo.Received(1).DownloadAsync(remoteUrl);
    }
}
