using System.Text;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.Configuration;
using Plutus.Test.Fakes;

namespace Plutus.Test.Configuration;

/// <summary>
/// Scaffolding a profile is a write, so it is checked for the two things a write can get wrong: clobbering a file
/// someone has already filled in, and reporting a failure as success. The writer comes from the file seam, so the
/// test captures what would land on disk without a disk.
/// </summary>
public class ProfileScaffolderTests
{
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\plutus-scaffold-tests" : "/plutus-scaffold-tests";

    [Fact]
    public void Scaffold_WritesTheTemplateAndCreatesTheDirectory()
    {
        var (scaffolder, detailProvider, written) = Scaffolder();
        var path = At("nested/.plutusrc");

        var result = scaffolder.Scaffold(path);

        result.Written.Should().BeTrue(result.Error);
        result.Path.Should().Be(path);
        written().Should().Be(new ProfileTemplate().Render());
        detailProvider.Received(1).CreateDirectoryForFile(Arg.Any<IFileInfo>());
        detailProvider.Received(1).CreateWriterForFile(Arg.Any<IFileInfo>(), Arg.Any<Encoding?>());
    }

    [Fact]
    public void Scaffold_WritesWithoutAByteOrderMark()
    {
        // The file is read by people and by other tools, so it must not start with a BOM.
        var (scaffolder, _, written) = Scaffolder();

        scaffolder.Scaffold(At(".plutusrc")).Written.Should().BeTrue();

        written().Should().NotStartWith("\uFEFF");
    }

    [Fact]
    public void Scaffold_RefusesToReplaceAnExistingProfile()
    {
        var (scaffolder, detailProvider, _) = Scaffolder(existing: [".plutusrc"]);

        var result = scaffolder.Scaffold(At(".plutusrc"));

        result.Written.Should().BeFalse();
        result.Error.Should().Contain("already exists").And.Contain("--force");
        detailProvider.DidNotReceive().CreateWriterForFile(Arg.Any<IFileInfo>(), Arg.Any<Encoding?>());
    }

    [Fact]
    public void Scaffold_WithOverwrite_ReplacesTheFile()
    {
        var (scaffolder, _, written) = Scaffolder(existing: [".plutusrc"]);

        var result = scaffolder.Scaffold(At(".plutusrc"), overwrite: true);

        result.Written.Should().BeTrue(result.Error);
        written().Should().Be(new ProfileTemplate().Render());
    }

    [Fact]
    public void Scaffold_WithAnEmptyPath_IsRefused()
    {
        var (scaffolder, _, _) = Scaffolder();

        var result = scaffolder.Scaffold("   ");

        result.Written.Should().BeFalse();
        result.Error.Should().Contain("no profile path");
    }

    [Fact]
    public void Scaffold_ReportsAWriteFailureInsteadOfClaimingSuccess()
    {
        var detailProvider = Substitute.For<IFileInfoDetailProvider>();
        detailProvider.CreateFile(Arg.Any<string>()).Returns(Substitute.For<IFileInfo>());
        detailProvider
            .When(provider => provider.CreateWriterForFile(Arg.Any<IFileInfo>(), Arg.Any<Encoding?>()))
            .Throw(new UnauthorizedAccessException("read-only"));
        var scaffolder = new ProfileScaffolder(
            detailProvider,
            new FileProviderAccess(new InMemoryFileProvider(), Root),
            new ProfileTemplate());

        var result = scaffolder.Scaffold(At(".plutusrc"));

        result.Written.Should().BeFalse();
        result.Error.Should().Contain("cannot write").And.Contain("read-only");
    }

    private static (ProfileScaffolder Scaffolder, IFileInfoDetailProvider Detail, Func<string> Written) Scaffolder(params string[] existing)
    {
        var files = new InMemoryFileProvider();
        foreach (var path in existing)
        {
            files.AddFile(path, "// an existing profile");
        }

        var buffer = new MemoryStream();
        var detailProvider = Substitute.For<IFileInfoDetailProvider>();
        var fileInfo = Substitute.For<IFileInfo>();
        detailProvider.CreateFile(Arg.Any<string>()).Returns(fileInfo);
        detailProvider
            .CreateWriterForFile(Arg.Any<IFileInfo>(), Arg.Any<Encoding?>())
            .Returns(_ => new StreamWriter(buffer, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true));

        var scaffolder = new ProfileScaffolder(detailProvider, new FileProviderAccess(files, Root), new ProfileTemplate());
        return (scaffolder, detailProvider, () => Encoding.UTF8.GetString(buffer.ToArray()));
    }

    private static string At(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
