using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;

namespace Plutus.Test;

public class FileInfoExtTests
{
    [Fact]
    public void IsSamePath_ShouldCompareEquivalentFullPaths()
    {
        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.PhysicalPath.Returns(Path.Combine(".", "folder", "file.txt"));

        fileInfo.IsSamePath(Path.GetFullPath(Path.Combine("folder", "file.txt"))).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsSamePath_ShouldReturnFalseForEmptyTarget(string? path)
    {
        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.PhysicalPath.Returns(Path.GetFullPath("file.txt"));

        fileInfo.IsSamePath(path!).Should().BeFalse();
    }

    [Fact]
    public void IsSamePath_ShouldReturnFalseWhenPhysicalPathIsMissing()
    {
        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.PhysicalPath.Returns((string?)null);

        fileInfo.IsSamePath("file.txt").Should().BeFalse();
    }
}
