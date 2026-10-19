using System.Text;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.BundleWriters.CacheReadProviders;
using Plutus.Domain.Minifier;
using Plutus.Test.RemoteRepository;

namespace Plutus.Test;

public class MarkdownBundleWriterTests
{
    private readonly IFileInfoDetailProvider _mockDetail;
    private readonly IMarkdownLanguageProvider _mockLangProvider = Substitute.For<IMarkdownLanguageProvider>();
    private readonly TestFileInfo _outputFile = new() { PhysicalPath = "out.md", Name = "out.md" };

    public MarkdownBundleWriterTests()
    {
        _mockDetail = new MockFileInfoDetailProvider
        {
            StreamFactory = _ => _outputFile.Stream
        };
        _mockLangProvider.GetLanguage(Arg.Any<string>()).Returns("text");
    }

    [Fact]
    public async Task WriteAsync_WithCodeFencesInContent_ShouldAdaptOuterFences()
    {
        // Arrange
        var mockFile = Substitute.For<IFileInfo>();
        mockFile.Name.Returns("Note.md");
        mockFile.Exists.Returns(true);

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
        var writer = new MarkdownBundleWriter(mockDetail, _mockLangProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null), new CacheReadProvider(Encoding.UTF8));
        var roots = CreateSingleFileRoot("Note.md", mockFile);

        // Act
        await writer.WriteAsync(roots, fileInfoOutput);

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
        mockFile.Exists.Returns(true);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var win1251 = Encoding.GetEncoding(1251);
        var cyrillicText = "Привет, Мир!";
        var byteContent = win1251.GetBytes(cyrillicText);

        mockFile.CreateReadStream().Returns(e => new MemoryStream(byteContent));
        mockFile.Length.Returns(byteContent.Length);

        var writer = new MarkdownBundleWriter(_mockDetail, _mockLangProvider, new IdentityCodeMinifier(), win1251, new BundleLimits(FileSize.FromBytes(4096), null), new CacheReadProvider(win1251));
        var roots = CreateSingleFileRoot("Russian.txt", mockFile);

        // Act
        await writer.WriteAsync(roots, _outputFile);

        // Assert
        var resultText = win1251.GetString(_outputFile.GetResetStream().ToArray());
        resultText.Should().Contain(cyrillicText);
    }

    [Fact]
    public async Task WriteAsync_WhenFileIsOversized_ShouldMarkTreeAndOmitContent()
    {
        // Arrange
        var mockFile = Substitute.For<IFileInfo>();
        mockFile.Name.Returns("big_file.txt");
        mockFile.Length.Returns(5000); // Размер 5000 байт

        var writer = new MarkdownBundleWriter(_mockDetail, _mockLangProvider, new IdentityCodeMinifier(), Encoding.UTF8, new BundleLimits(FileSize.FromBytes(4096), null), new CacheReadProvider(Encoding.UTF8));
        var roots = CreateSingleFileRoot("big_file.txt", mockFile);

        // Act
        // Ограничение всего 1000 байт
        await writer.WriteAsync(roots, _outputFile);

        // Assert
        var resultText = Encoding.UTF8.GetString(_outputFile.GetResetStream().ToArray());
        resultText.Should().Contain("big_file.txt [4.9 KB]");
        resultText.Should().Contain("big_file.txt [LARGE 4.9 KB]");
        resultText.Should().NotContain("## FILE: `big_file.txt`");
        roots[0].Item4.Status.Should().Be(NodeStatus.OversizedFile);
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
