using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class TreeRendererTests
{
    [Fact]
    public void RenderTree_ShouldDrawCorrectBranches_AndSkipOutputFile()
    {
        // Arrange
        var mockProvider = Substitute.For<IFileProvider>();
        var mockDetailProvider = Substitute.For<IFileInfoDetailProvider>();
        var mockMatcher = Substitute.For<IIgnoreMatcher>();

        // Симулируем корень с одной папкой "src" и одним файлом "README.md"
        var rootContents = Substitute.For<IDirectoryContents>();
        var dirSrc = CreateMockFile("src", isDirectory: true);
        var fileReadme = CreateMockFile("README.md", isDirectory: false, length: 100);

        rootContents.Exists.Returns(true);
        rootContents.GetEnumerator().Returns(new List<IFileInfo> { dirSrc, fileReadme }.GetEnumerator());
        mockProvider.GetDirectoryContents("").Returns(rootContents);

        // Симулируем содержимое папки "src" (в ней лежит Program.cs и генерируемый бандл)
        var srcContents = Substitute.For<IDirectoryContents>();
        var fileProgram = CreateMockFile("Program.cs", isDirectory: false, length: 200, physicalPath: @"C:\Project\src\Program.cs");
        var fileOutput = CreateMockFile("bundle.md", isDirectory: false, length: 0, physicalPath: @"C:\Project\src\bundle.md");

        srcContents.Exists.Returns(true);
        srcContents.GetEnumerator().Returns(new List<IFileInfo> { fileProgram, fileOutput }.GetEnumerator());
        mockProvider.GetDirectoryContents("src").Returns(srcContents);

        var renderer = new TreeRenderer(mockMatcher, mockDetailProvider);

        // Act
        // Передаем путь к бандлу @"C:\Project\src\bundle.md", чтобы проверить его исключение
        var result = renderer.RenderTree(mockProvider, "RootNode", @"C:\Project\src\bundle.md", maxFileSize: 1024);

        // Assert
        result.Should().ContainInOrder(
            "RootNode/",
            "├── src/",
            "│   └── Program.cs [200 B]", // Единственный валидный файл в подпапке (стал последним)
            "└── README.md [100 B]"      // Последний элемент корня
        );

        // Проверяем, что файл вывода не попал в отрисовку
        result.Should().NotContain("bundle.md");
    }

    private static IFileInfo CreateMockFile(string name, bool isDirectory, long length = 0, string? physicalPath = null)
    {
        var file = Substitute.For<IFileInfo>();
        file.Name.Returns(name);
        file.IsDirectory.Returns(isDirectory);
        file.Length.Returns(length);
        file.PhysicalPath.Returns(physicalPath);
        return file;
    }
}
