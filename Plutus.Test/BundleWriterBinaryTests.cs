using System.Text;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.Minifier;

namespace Plutus.Test;

public class BundleWriterBinaryTests
{
    [Fact]
    public async Task ReadTextSafeAsync_ThroughPrivateReflectionOrCollector_ShouldDetectBinaryFile()
    {
        // Arrange
        var mockFile = Substitute.For<IFileInfo>();
        mockFile.Name.Returns("image.png");
        mockFile.Length.Returns(20);

        // Симулируем бинарный поток, содержащий нулевой байт (индикатор бинарника)
        var binaryBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x1A, 0x0A };
        var memoryStream = new MemoryStream(binaryBytes);
        mockFile.CreateReadStream().Returns(e => new MemoryStream(memoryStream.ToArray()));

        var mockDetail = Substitute.For<IFileInfoDetailProvider>();

        var writer = new BundleWriter(mockDetail, new MarkdownLanguageProvider(), IdentityCodeMinifier.Instance);

        var fileInfoOutput = new TestFileInfo
        {
            PhysicalPath = "dir/my_bundle.md",
            Name = "my_bundle.md"
        };
        mockDetail.CreateWriterForFile(fileInfoOutput, Arg.Any<Encoding?>())
            .Returns(c => new StreamWriter(fileInfoOutput.Stream, leaveOpen: true));
        var roots = new List<(IFileProvider, string, string, ProjectNode)>
        {
            (new NullFileProvider(), string.Empty, string.Empty, new ProjectNode
            {
                Name = "image.png",
                RelativePath = "",
                IsDirectory = false,
                Status = NodeStatus.Included,
                FileInfo = mockFile
            }),
        };

        // Act
        var action = async () => await writer.WriteAsync(roots, fileInfoOutput, maxFileSize: FileSize.FromBytes(1024));

        // Assert
        await action.Should().NotThrowAsync();

        await using var stream = fileInfoOutput.CreateReadStream();
        using var reader = new StreamReader(stream);
        var resultText = await reader.ReadToEndAsync();
        resultText.Should().Contain("[skipped: binary file]");
    }

    private sealed class TestFileInfo : IFileInfo
    {
        public MemoryStream Stream { get; } = new();

        public Stream CreateReadStream()
        {
            Stream.Position = 0;
            return Stream;
        }

        public bool Exists => true;
        public long Length => Stream.Length;
        public required string? PhysicalPath { get; set; }
        public required string Name { get; set; }
        public DateTimeOffset LastModified => new DateTimeOffset(2026, 10, 6, 20, 14, 22, TimeSpan.Zero);
        public bool IsDirectory => false;
    }
}
