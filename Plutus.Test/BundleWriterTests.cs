using System.Text;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;

namespace Plutus.Test;

public class BundleWriterTests
{
    private readonly IFileInfoDetailProvider _mockDetail = Substitute.For<IFileInfoDetailProvider>();
    private readonly IMarkdownLanguageProvider _mockLangProvider = Substitute.For<IMarkdownLanguageProvider>();
    private readonly TestFileInfo _outputFile = new() { PhysicalPath = "out.md", Name = "out.md" };

    public BundleWriterTests()
    {
        _mockDetail.CreateWriterForFile(_outputFile, Arg.Any<Encoding?>())
            .Returns(_ => new StreamWriter(_outputFile.Stream, leaveOpen: true));
        _mockLangProvider.GetLanguage(Arg.Any<string>()).Returns("text");
    }

    [Fact]
    public async Task WriteAsync_WithCodeFencesInContent_ShouldAdaptOuterFences()
    {
        // Arrange
        var mockFile = Substitute.For<IFileInfo>();
        mockFile.Name.Returns("Note.md");

        // Внутри контента уже есть стандартные 3 кавычки
        var innerContent = "Notes:\n```csharp\nConsole.WriteLine();\n```";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(innerContent));
        mockFile.CreateReadStream().Returns(_ => new MemoryStream(stream.ToArray()));
        mockFile.Length.Returns(stream.Length);

        var mockDetail = Substitute.For<IFileInfoDetailProvider>();
        var fileInfoOutput = new TestFileInfo { PhysicalPath = "out.md", Name = "out.md" };
        mockDetail.CreateWriterForFile(fileInfoOutput, Arg.Any<Encoding?>())
            .Returns(_ => new StreamWriter(fileInfoOutput.Stream, leaveOpen: true));

        // Важно: BundleWriter сам определит язык "markdown" для расширения .md
        var writer = new BundleWriter(mockDetail, _mockLangProvider);
        var roots = CreateSingleFileRoot("Note.md", mockFile);

        // Act
        await writer.WriteAsync(roots, fileInfoOutput, FileSize.FromBytes(1024));

        // Assert
        var resultText = Encoding.UTF8.GetString(fileInfoOutput.Stream.ToArray());

        // ИСПРАВЛЕНИЕ: Внешние разделители должны увеличиться до 4 кавычек, а язык будет "text"
        resultText.Should().Contain("````text").And.Contain("````");
    }

    [Fact]
    public async Task WriteAsync_WithWindows1251Encoding_ShouldDecodeCorrectly()
    {
        // Arrange
        var mockFile = Substitute.For<IFileInfo>();
        mockFile.Name.Returns("Russian.txt");

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var win1251 = Encoding.GetEncoding(1251);
        var cyrillicText = "Привет, Мир!";
        var stream = new MemoryStream(win1251.GetBytes(cyrillicText));

        mockFile.CreateReadStream().Returns(e => new MemoryStream(stream.ToArray()));
        mockFile.Length.Returns(stream.Length);

        var writer = new BundleWriter(_mockDetail, _mockLangProvider);
        var roots = CreateSingleFileRoot("Russian.txt", mockFile);

        // Act
        await writer.WriteAsync(roots, _outputFile, FileSize.FromBytes(1024));

        // Assert
        var resultText = Encoding.UTF8.GetString(_outputFile.GetResetStream().ToArray());
        resultText.Should().Contain("Привет, Мир!");
    }

    [Fact]
    public async Task WriteAsync_WhenFileIsOversized_ShouldWriteSkippedMarker()
    {
        // Arrange
        var mockFile = Substitute.For<IFileInfo>();
        mockFile.Name.Returns("big_file.txt");
        mockFile.Length.Returns(5000); // Размер 5000 байт

        var writer = new BundleWriter(_mockDetail, _mockLangProvider);
        var roots = CreateSingleFileRoot("big_file.txt", mockFile);

        // Act
        // Ограничение всего 1000 байт
        await writer.WriteAsync(roots, _outputFile, FileSize.FromBytes(1000));

        // Assert
        var resultText = Encoding.UTF8.GetString(_outputFile.GetResetStream().ToArray());
        resultText.Should().Contain("[skipped: file is larger than max-file-size");
        mockFile.DidNotReceive().CreateReadStream(); // Контент вообще не должен считываться
    }

    private List<(IFileProvider, string, string, ProjectNode)> CreateSingleFileRoot(string name, IFileInfo fileInfo)
    {
        return [
            (new NullFileProvider(), "Root", "Root", new ProjectNode
            {
                Name = name,
                RelativePath = name,
                IsDirectory = false,
                Status = NodeStatus.Included,
                FileInfo = fileInfo
            })
        ];
    }
}
