using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.Diffing;
using Plutus.Domain.Tokens;
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

    private const string UntrackedDiffText =
        """
        diff --git a/src/New.cs b/src/New.cs
        new file mode 100644
        index 0000000..3333333
        --- /dev/null
        +++ b/src/New.cs
        @@ -0,0 +1 @@
        +public class New;
        """;

    private const string SecondUntrackedDiffText =
        """
        diff --git a/docs/Release Notes.md b/docs/Release Notes.md
        new file mode 100644
        index 0000000..4444444
        --- /dev/null
        +++ b/docs/Release Notes.md
        @@ -0,0 +1 @@
        +# Notes
        """;

    [Fact]
    public async Task ResolveAsync_WhenNoTargetIsConfigured_ShouldCompareWorkingTreeWithResolvedHead()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", DiffText);
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

        result.Should().NotBeNull();
        result!.Context.ComparisonBase.Should().Be("headsha");
        result.Context.TrackedChunks.Should().ContainSingle().Which.Path.Should().Be("src/Core.cs");
        result.Context.TrackedChunks[0].Kind.Should().Be(ChangeKind.Modified);
        result.Context.TrackedChunks[0].AddedLines.Should().Be(1);

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
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

        result.Should().NotBeNull();
        result!.Context.ComparisonBase.Should().Be("origin/main");
        result.Context.TrackedChunks.Should().BeEmpty();

        // The upstream answer is used directly, so HEAD never has to be resolved.
        executor.Calls.Should().HaveCount(3);
        executor.Calls[2].Arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-renames");
    }

    [Fact]
    public async Task ResolveAsync_WhenTargetIsConfigured_ShouldKeepTheRefAndUseMergeBaseRangeSemantics()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", DiffText);
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions { Target = "origin/main" }, [Root("/repo")]);

        result.Should().NotBeNull();

        // The ref is resolved by git itself, so the merge base is computed when the diff runs.
        result!.Context.ComparisonBase.Should().Be("origin/main");
        executor.Calls[1].Arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-renames", "--merge-base", "origin/main");
    }

    [Fact]
    public async Task ResolveAsync_WhenARangeIsConfigured_ShouldCompareBothCommitsAndSkipUpstreamLookup()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", DiffText);
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(
            new GitDiffOptions { Target = "v1.0.0", TargetTo = "v1.1.0" },
            [Root("/repo")]);

        result.Should().NotBeNull();
        result!.Context.ComparisonBase.Should().Be("v1.0.0");
        executor.Calls.Should().HaveCount(2);
        executor.Calls[1].Arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-renames", "v1.0.0", "v1.1.0");
    }

    [Fact]
    public async Task ResolveAsync_WhenTargetDoesNotExist_ShouldSurfaceTheGitFailure()
    {
        var executor = StubGitCommandExecutor.Seeded(
            StubGitCommandExecutor.Success("/repo"),
            StubGitCommandExecutor.Failure("fatal: ambiguous argument 'nope...'"));
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions { Target = "nope" }, [Root("/repo")]);

        result.Should().BeNull();
        executor.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResolveAsync_WhenProjectPathIsNotARepository_ShouldReturnNull()
    {
        var executor = StubGitCommandExecutor.Seeded(StubGitCommandExecutor.Failure("not a git repository"));
        var provider = CreateProvider(executor);

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

        var result = await CreateProvider(executor).ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

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
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenProjectRootIsASubdirectory_ShouldMapPathsAndDiffTextToThatRoot()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", DiffText);
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo/src")]);

        result.Should().NotBeNull();
        result!.Context.TrackedPathsFor("/repo/src").Should().Equal("Core.cs");
        result.Context.TrackedChunksFor("/repo/src").Should().ContainSingle()
            .Which.Text.Should().Contain("diff --git a/Core.cs b/Core.cs");
    }

    [Fact]
    public async Task ResolveAsync_WhenChangedFileIsOutsideTheRoot_ShouldExcludeIt()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", DiffText);
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo/tests")]);

        result.Should().NotBeNull();
        result!.Context.TrackedChunks.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenProjectPathIsOutsideTheRepository_ShouldRejectIt()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo");
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/elsewhere")]);

        result.Should().BeNull();

        // Discovery runs at the project path, and no diff is attempted for a path outside the repository.
        executor.Calls.Should().ContainSingle();
        executor.Calls[0].WorkingDirectory.Should().Be("/elsewhere");
    }

    [Fact]
    public async Task ResolveAsync_WhenUntrackedFilesAreRequested_ShouldGenerateAndKeepTheirDiffs()
    {
        var executor = StubGitCommandExecutor.Succeeding(
            "/repo",
            string.Empty,
            "headsha",
            DiffText,
            "src/New.cs\0docs/Release Notes.md\0",
            UntrackedDiffText,
            SecondUntrackedDiffText);
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions { IncludeUntracked = true }, [Root("/repo")]);

        result.Should().NotBeNull();
        result!.Context.UntrackedPathsFor("/repo").Should().Equal("src/New.cs", "docs/Release Notes.md");

        // Tracked changes and generated diffs stay apart.
        result.Context.TrackedChunks.Should().ContainSingle().Which.Path.Should().Be("src/Core.cs");
        result.Context.UntrackedChunks.Should().HaveCount(2);

        executor.Calls.Should().HaveCount(7);
        executor.Calls[4].Arguments.Should().Equal("ls-files", "-z", "--others", "--exclude-standard");
        executor.Calls[5].Arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-index", "--", "/dev/null", "src/New.cs");
        executor.Calls[6].Arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-index", "--", "/dev/null", "docs/Release Notes.md");
    }

    [Fact]
    public async Task ResolveAsync_WhenAnUntrackedFileDiffCannotBeGenerated_ShouldKeepTheRest()
    {
        var executor = StubGitCommandExecutor.Seeded(
            StubGitCommandExecutor.Success("/repo"),
            StubGitCommandExecutor.Failure(),
            StubGitCommandExecutor.Success("headsha"),
            StubGitCommandExecutor.Success(DiffText),
            StubGitCommandExecutor.Success("src/New.cs\0"),
            StubGitCommandExecutor.Failure("error: unable to read file"));
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions { IncludeUntracked = true }, [Root("/repo")]);

        // The unreadable file simply contributes no diff, and the bundle is not lost.
        result.Should().NotBeNull();
        result!.Context.UntrackedChunks.Should().BeEmpty();
        result.Context.TrackedChunks.Should().ContainSingle();
    }

    [Fact]
    public async Task ResolveAsync_WithoutTheUntrackedOption_ShouldNotQueryUntrackedFiles()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", DiffText);
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo")]);

        result.Should().NotBeNull();
        result!.Context.UntrackedChunks.Should().BeEmpty();
        executor.Calls.Should().HaveCount(4);
    }

    [Fact]
    public async Task ResolveAsync_WhenOnlyOneOfSeveralRootsIsOutsideTheRepository_ShouldRejectTheRun()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo");
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), [Root("/repo/src"), Root("/elsewhere")]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_WithNoRoots_ShouldFail()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo");
        var provider = CreateProvider(executor);

        var result = await provider.ResolveAsync(new GitDiffOptions(), []);

        result.Should().BeNull();
        executor.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenTheDiffExceedsTheTokenBudget_ShouldOmitOnlyTheChunkThatDoesNotFit()
    {
        // Two chunks with clearly different weights, so the budget can keep one and drop the other.
        var twoFileDiff =
            """
            diff --git a/src/Small.cs b/src/Small.cs
            --- a/src/Small.cs
            +++ b/src/Small.cs
            @@ -1 +1 @@
            -a
            +b
            """ + "\n" + """
            diff --git a/src/Large.cs b/src/Large.cs
            --- a/src/Large.cs
            +++ b/src/Large.cs
            @@ -1 +1 @@
            -a
            +b
            """ + string.Concat(Enumerable.Repeat("\n+padding line to add weight to this chunk\n", 40));

        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", twoFileDiff);
        var budget = new TokenBudgetOptions(40, TokenOverflowPolicy.Select, TokenSelectionStrategy.PathOrder);
        var provider = CreateProvider(executor, budget);
        var options = new GitDiffOptions { TokenBudget = budget };

        var result = await provider.ResolveAsync(options, [Root("/repo")]);

        result.Should().NotBeNull();
        result!.Context.TrackedChunks.Should().HaveCount(2);

        var kept = result.Context.TrackedChunks.Where(chunk => !chunk.IsSkipped).ToArray();
        var omitted = result.Context.TrackedChunks.Where(chunk => chunk.IsSkipped).ToArray();

        kept.Should().ContainSingle().Which.Path.Should().Be("src/Small.cs");
        omitted.Should().ContainSingle().Which.Path.Should().Be("src/Large.cs");
        omitted[0].SkipReason.Should().Contain("omitted by token budget");
        result.Excluded.Should().Equal("src/Large.cs");

        result.Summary.Should().NotBeNull();
        result.Summary!.SelectedTokens.Should().BeLessThanOrEqualTo(40);
        result.Summary.CandidateTokens.Should().BeGreaterThan(result.Summary.SelectedTokens);
    }

    [Fact]
    public async Task ResolveAsync_WhenTheDiffExceedsTheTokenBudgetUnderFailPolicy_ShouldReturnNull()
    {
        var executor = StubGitCommandExecutor.Succeeding("/repo", string.Empty, "headsha", DiffText);
        var budget = new TokenBudgetOptions(1, TokenOverflowPolicy.Fail);
        var provider = CreateProvider(executor, budget);
        var options = new GitDiffOptions { TokenBudget = budget };

        var result = await provider.ResolveAsync(options, [Root("/repo")]);

        result.Should().BeNull();
    }

    private static GitDiffContextProvider CreateProvider(IGitCommandExecutor executor, TokenBudgetOptions? budget = null) =>
        new(executor, new DiffScopeLimiter(new BundleLimits(FileSize.FromBytes(1024), budget), ignorePathCase: true));

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
