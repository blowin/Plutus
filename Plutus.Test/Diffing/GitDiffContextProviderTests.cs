using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain.Diffing;
using Plutus.Infrastructure;
using Plutus.Infrastructure.Git;

namespace Plutus.Test.Diffing;

public class GitDiffContextProviderTests
{
    private const string DiffText =
        """
        diff --git a/src/Core.cs b/src/Core.cs
        index 1111111..2222222 100644
        --- a/src/Core.cs
        +++ b/src/Core.cs
        @@ -1 +1 @@
        -old
        +new
        """;

    [Fact]
    public async Task ResolveAsync_WhenNoTargetIsConfigured_ShouldCompareWorkingTreeWithResolvedHead()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", DiffText);
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

        result.Should().NotBeNull();
        result!.ComparisonBase.Should().Be("headsha");
        result.Files.Should().ContainSingle().Which.Should().Be(new ChangedFile("src/Core.cs", ChangeKind.Modified));
        result.RawText.Should().Be(DiffText);

        executor.Calls.Select(call => string.Join(' ', call.Arguments)).Should().Equal(
            "rev-parse --show-toplevel",
            "rev-parse --abbrev-ref --symbolic-full-name @{upstream}",
            "rev-parse --verify --quiet HEAD^{commit}",
            "diff --no-color --no-ext-diff --no-renames");

        // Discovery runs at the project path, and every later call runs at the resolved repository root.
        executor.Calls.Should().OnlyContain(call => call.WorkingDirectory == "/repo");
    }

    [Fact]
    public async Task ResolveAsync_WhenAnUpstreamBranchExists_ShouldUseItAsComparisonBase()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", "origin/main", string.Empty);
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

        result.Should().NotBeNull();
        result!.ComparisonBase.Should().Be("origin/main");
        result.Files.Should().BeEmpty();

        // The upstream answer is used directly, so HEAD never has to be resolved.
        executor.Calls.Should().HaveCount(3);
        executor.Calls[2].Arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-renames");
    }

    [Fact]
    public async Task ResolveAsync_WhenTargetIsConfigured_ShouldKeepTheRefAndUseMergeBaseRangeSemantics()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", DiffText);
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions { Target = "origin/main" }, [Root("/repo")]);

        result.Should().NotBeNull();

        // The ref is resolved by git itself, so the merge base is computed when the diff runs.
        result!.ComparisonBase.Should().Be("origin/main");
        executor.Calls[1].Arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-renames", "--merge-base", "origin/main");
    }

    [Fact]
    public async Task ResolveAsync_WhenTargetDoesNotExist_ShouldSurfaceTheGitFailure()
    {
        var executor = StubGitCommandExecutor.Seeded(
            StubGitCommandExecutor.Success("/repo"),
            StubGitCommandExecutor.Failure("fatal: ambiguous argument 'nope...'"));
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions { Target = "nope" }, [Root("/repo")]);

        result.Should().BeNull();
        executor.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResolveAsync_WhenProjectPathIsNotARepository_ShouldReturnNull()
    {
        var executor = StubGitCommandExecutor.Seeded(StubGitCommandExecutor.Failure("not a git repository"));
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/plain/folder")]);

        result.Should().BeNull();
        executor.Calls.Should().ContainSingle();
        executor.Calls[0].WorkingDirectory.Should().Be("/plain/folder");
    }

    [Fact]
    public async Task ResolveAsync_WhenGitCannotBeStarted_ShouldReportFailureInsteadOfThrowing()
    {
        var executor = Substitute.For<IGitCommandExecutor>();
        executor.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns<Task<GitCommandResult>>(_ => throw new InvalidOperationException("git was not found"));

        var result = await new GitDiffContextProvider(executor).ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenDiffFails_ShouldReturnNull()
    {
        var executor = StubGitCommandExecutor.Seeded(
            StubGitCommandExecutor.Success("/repo"),
            StubGitCommandExecutor.Failure(),
            StubGitCommandExecutor.Success("headsha"),
            StubGitCommandExecutor.Failure("fatal: bad revision"));
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenProjectRootIsASubdirectory_ShouldMapPathsRelativeToThatRoot()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", DiffText);
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo/src")]);

        result.Should().NotBeNull();
        result!.Files.Should().ContainSingle().Which.Path.Should().Be("Core.cs");

        // The mapped group is keyed by the project root, so the writer finds it without re-deriving paths.
        var group = result.FileGroups.Should().ContainSingle().Subject;
        group.RootPath.Should().Be("/repo/src");
        result.PathsFor("/repo/src").Should().Equal("Core.cs");
        result.RawText.Should().Be(DiffText);
    }

    [Fact]
    public async Task ResolveAsync_WhenChangedFileIsOutsideTheRoot_ShouldExcludeIt()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", DiffText);
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo/tests")]);

        result.Should().NotBeNull();
        result!.Files.Should().BeEmpty();
        result.RawText.Should().Be(DiffText);
    }

    [Fact]
    public async Task ResolveAsync_WhenProjectPathIsOutsideTheRepository_ShouldRejectIt()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo");
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/elsewhere")]);

        result.Should().BeNull();

        // Discovery runs at the project path, and no diff is attempted for a path outside the repository.
        executor.Calls.Should().ContainSingle();
        executor.Calls[0].WorkingDirectory.Should().Be("/elsewhere");
    }

    [Fact]
    public async Task ResolveAsync_WhenOnlyOneOfSeveralRootsIsOutsideTheRepository_ShouldRejectTheRun()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo");
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo/src"), Root("/elsewhere")]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_WithNoRoots_ShouldFail()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo");
        var provider = new GitDiffContextProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), []);

        result.Should().BeNull();
        executor.Calls.Should().BeEmpty();
    }

    private static IFileInfo Root(string physicalPath)
    {
        var root = Substitute.For<IFileInfo>();
        root.Name.Returns(Path.GetFileName(physicalPath) ?? physicalPath);
        root.PhysicalPath.Returns(physicalPath);
        root.IsDirectory.Returns(true);
        root.Exists.Returns(true);
        return root;
    }
}
