using System.Text;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens;

namespace Plutus.Test;

public class TokenBudgetTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 2)]
    [InlineData("Привет", 2)]
    public void Estimate_UsesDocumentedFourScalarsPerTokenHeuristic(string content, int expected)
    {
        TokenEstimator.Estimate(content).Should().Be(expected);
    }

    [Fact]
    public async Task SelectPolicy_IncludesWholeFilesDeterministically_AndReportsEstimates()
    {
        var output = new TestFileInfo { PhysicalPath = "out.md", Name = "out.md" };
        var detail = Substitute.For<IFileInfoDetailProvider>();
        detail.CreateWriterForFile(output, Arg.Any<Encoding?>())
            .Returns(_ => new StreamWriter(output.Stream, leaveOpen: true));
        var languages = Substitute.For<IMarkdownLanguageProvider>();
        languages.GetLanguage(Arg.Any<string>()).Returns("text");

        var writer = new MarkdownBundleWriter(detail, languages, IdentityCodeMinifier.Instance, Encoding.UTF8);
        var a = CreateFile("a.txt", "AAAA");
        var b = CreateFile("b.txt", "BBBB");
        var root = new ProjectNode
        {
            Name = "root",
            RelativePath = "",
            IsDirectory = true,
            Status = NodeStatus.Included
        };
        root.Children.Add(new ProjectNode { Name = "b.txt", RelativePath = "b.txt", IsDirectory = false, Status = NodeStatus.Included, FileInfo = b });
        root.Children.Add(new ProjectNode { Name = "a.txt", RelativePath = "a.txt", IsDirectory = false, Status = NodeStatus.Included, FileInfo = a });

        await writer.WriteAsync(
            [(new NullFileProvider(), "root", "root", root)],
            output,
            RunAppOptionsFactory.Create(FileSize.FromBytes(1024),1, "select", "path-order"));

        var text = Encoding.UTF8.GetString(output.Stream.ToArray());
        text.Should().Contain("Token estimate method:").And.Contain("a.txt").And.Contain("b.txt");
        text.Should().Contain("AAAA");
        text.Should().Contain("omitted by token budget");
        text.Should().NotContain("BBBB");
    }

    [Fact]
    public async Task WarnPolicy_WritesAllEligibleFiles_WhenBudgetIsExceeded()
    {
        var output = new TestFileInfo { PhysicalPath = "out.md", Name = "out.md" };
        var detail = Substitute.For<IFileInfoDetailProvider>();
        detail.CreateWriterForFile(output, Arg.Any<Encoding?>())
            .Returns(_ => new StreamWriter(output.Stream, leaveOpen: true));
        var languages = Substitute.For<IMarkdownLanguageProvider>();
        languages.GetLanguage(Arg.Any<string>()).Returns("text");
        var writer = new MarkdownBundleWriter(detail, languages, IdentityCodeMinifier.Instance, Encoding.UTF8);
        var a = CreateFile("a.txt", "AAAA");
        var b = CreateFile("b.txt", "BBBB");
        var root = new ProjectNode
        {
            Name = "root", RelativePath = "", IsDirectory = true, Status = NodeStatus.Included,
            Children =
            {
                new ProjectNode { Name = "a.txt", RelativePath = "a.txt", IsDirectory = false, Status = NodeStatus.Included, FileInfo = a },
                new ProjectNode { Name = "b.txt", RelativePath = "b.txt", IsDirectory = false, Status = NodeStatus.Included, FileInfo = b }
            }
        };

        await writer.WriteAsync(
            [(new NullFileProvider(), "root", "root", root)], output,
            RunAppOptionsFactory.Create(FileSize.FromBytes(1024), 1, "warn", "path-order"));

        var text = Encoding.UTF8.GetString(output.Stream.ToArray());
        text.Should().Contain("AAAA").And.Contain("BBBB");
        text.Should().Contain("Estimated source tokens: `2`");
    }

    [Fact]
    public async Task FailPolicy_ThrowsBeforeWritingBundle_WhenBudgetIsExceeded()
    {
        var output = new TestFileInfo { PhysicalPath = "out.md", Name = "out.md" };
        var detail = Substitute.For<IFileInfoDetailProvider>();
        detail.CreateWriterForFile(output, Arg.Any<Encoding?>())
            .Returns(_ => new StreamWriter(output.Stream, leaveOpen: true));
        var languages = Substitute.For<IMarkdownLanguageProvider>();
        languages.GetLanguage(Arg.Any<string>()).Returns("text");
        var writer = new MarkdownBundleWriter(detail, languages, IdentityCodeMinifier.Instance, Encoding.UTF8);
        var file = CreateFile("a.txt", "AAAAA");
        var root = new ProjectNode
        {
            Name = "root", RelativePath = "", IsDirectory = true, Status = NodeStatus.Included,
            Children = { new ProjectNode { Name = "a.txt", RelativePath = "a.txt", IsDirectory = false, Status = NodeStatus.Included, FileInfo = file } }
        };

        var act = async () => await writer.WriteAsync(
            [(new NullFileProvider(), "root", "root", root)], output,
            RunAppOptionsFactory.Create(FileSize.FromBytes(1024), 1, "fail", "path-order"));

        await act.Should().ThrowAsync<TokenBudgetExceededException>();
        output.Stream.Length.Should().Be(0);
    }

    private static IFileInfo CreateFile(string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = Substitute.For<IFileInfo>();
        file.Name.Returns(name);
        file.Length.Returns(bytes.Length);
        file.Exists.Returns(true);
        file.IsDirectory.Returns(false);
        file.CreateReadStream().Returns(_ => new MemoryStream(bytes, writable: false));
        return file;
    }
}
