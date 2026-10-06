using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;

namespace Plutus.Test;

public class TreeRendererTests
{
    [Fact]
    public void RenderTree_ShouldDrawCorrectBranchesBasedOnProjectNodeTree()
    {
        // Arrange
        // Строим дерево проекта в памяти с помощью простых POCO-объектов
        var rootNode = new ProjectNode
        {
            Name = "RootNode/",
            RelativePath = "",
            IsDirectory = true,
            Status = NodeStatus.Included
        };

        var srcDir = new ProjectNode
        {
            Name = "src",
            RelativePath = "src",
            IsDirectory = true,
            Status = NodeStatus.Included
        };

        var fileProgram = new ProjectNode
        {
            Name = "Program.cs",
            RelativePath = "src/Program.cs",
            IsDirectory = false,
            Status = NodeStatus.Included,
            FileInfo = CreateMockFile("Program.cs", length: 200)
        };

        var fileReadme = new ProjectNode
        {
            Name = "README.md",
            RelativePath = "README.md",
            IsDirectory = false,
            Status = NodeStatus.Included,
            FileInfo = CreateMockFile("README.md", length: 100)
        };

        // Собираем иерархию
        srcDir.Children.Add(fileProgram);
        rootNode.Children.Add(srcDir);
        rootNode.Children.Add(fileReadme);

        var renderer = new TreeRenderer();

        // Act
        var result = renderer.RenderTree(rootNode);

        // Assert
        result.Should().ContainInOrder(
            "RootNode/",
            "├── src/",
            "│   └── Program.cs [200 B]",
            "└── README.md [100 B]"
        );
    }

    private static IFileInfo CreateMockFile(string name, long length)
    {
        var file = Substitute.For<IFileInfo>();
        file.Name.Returns(name);
        file.IsDirectory.Returns(false);
        file.Length.Returns(length);
        file.Exists.Returns(true);
        return file;
    }
}
