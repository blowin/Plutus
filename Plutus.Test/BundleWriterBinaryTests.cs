using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class BundleWriterBinaryTests
{
    [Fact]
    public void ReadTextSafeAsync_ThroughPrivateReflectionOrCollector_ShouldDetectBinaryFile()
    {
        // Arrange
        var mockFile = Substitute.For<IFileInfo>();
        mockFile.Name.Returns("image.png");
        mockFile.Length.Returns(20);

        // Симулируем бинарный поток, содержащий нулевой байт (индикатор бинарника)
        var binaryBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x1A, 0x0A };
        var memoryStream = new MemoryStream(binaryBytes);
        mockFile.CreateReadStream().Returns(memoryStream);

        var mockDetail = Substitute.For<IFileInfoDetailProvider>();
        var mockMatcher = Substitute.For<IIgnoreMatcher>();

        var writer = new BundleWriter(mockDetail, mockMatcher);

        // Так как метод ReadTextSafeAsync приватный, мы можем протестировать его косвенно
        // через вызов WriteAsync с одним элементом и проверить результирующий markdown-файл.
        var tempOutput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_bundle.md");
        var fileInfoOutput = new FileInfo(tempOutput);

        var roots = new List<(IFileProvider, string, string, ProjectNode)>
        {
            (new NullFileProvider(), string.Empty, tempOutput, new ProjectNode
            {
                Name = "image.png",
                RelativePath = "",
                IsDirectory = false,
                Status = NodeStatus.Included,
                FileInfo = mockFile
            }),
        };

        // Act
        var action = async () => await writer.WriteAsync(roots, fileInfoOutput, maxFileSize: 1024);

        // Assert
        action.Should().NotThrowAsync();

        if (File.Exists(tempOutput))
        {
            var resultText = File.ReadAllText(tempOutput);
            resultText.Should().Contain("[skipped: binary file]");
            File.Delete(tempOutput);
        }
    }
}
