using FluentAssertions;
using Plutus.Domain.Diffing;

namespace Plutus.Test.Diffing;

public class GitDiffChunkerTests
{
    private const string TwoFileDiff =
        """
        diff --git a/src/Core.cs b/src/Core.cs
        index 1111111..2222222 100644
        --- a/src/Core.cs
        +++ b/src/Core.cs
        @@ -1,3 +1,3 @@
        -var old = 1;
        +var updated = 2;
         unchanged
        diff --git a/src/New.cs b/src/New.cs
        new file mode 100644
        index 0000000..3333333
        --- /dev/null
        +++ b/src/New.cs
        @@ -0,0 +1,2 @@
        +public class New;
        +public class Also;
        """;

    [Fact]
    public void Split_WithTwoFiles_ShouldProduceOneChunkPerFile()
    {
        var chunks = GitDiffChunker.Split(TwoFileDiff);

        chunks.Should().HaveCount(2);
        chunks[0].RepositoryPath.Should().Be("src/Core.cs");
        chunks[0].Kind.Should().Be(ChangeKind.Modified);
        chunks[1].RepositoryPath.Should().Be("src/New.cs");
        chunks[1].Kind.Should().Be(ChangeKind.Added);
    }

    [Fact]
    public void Split_ShouldCountChangedLinesAndIgnoreFileHeaders()
    {
        var chunks = GitDiffChunker.Split(TwoFileDiff);

        chunks[0].AddedLines.Should().Be(1);
        chunks[0].DeletedLines.Should().Be(1);
        chunks[1].AddedLines.Should().Be(2);
        chunks[1].DeletedLines.Should().Be(0);
        chunks[0].LineCount.Should().Be(2);
    }

    [Fact]
    public void Split_ShouldKeepEachChunkAsAStandalonePatchWithItsHeader()
    {
        var chunks = GitDiffChunker.Split(TwoFileDiff);

        chunks[1].Text.Should().StartWith("diff --git a/src/New.cs b/src/New.cs");
        chunks[1].Text.Should().Contain("new file mode 100644").And.Contain("+public class Also;");
        chunks[1].Text.Should().NotContain("src/Core.cs");
    }

    [Fact]
    public void Split_WhenDiffUsesWindowsSeparators_ShouldNormalizeToLineFeed()
    {
        var chunks = GitDiffChunker.Split(TwoFileDiff.Replace("\n", "\r\n"));

        chunks.Should().HaveCount(2);
        chunks[0].Text.Should().NotContain("\r");
        chunks[0].AddedLines.Should().Be(1);
    }

    [Fact]
    public void Split_WhenDiffIsEmpty_ShouldReturnNoChunks()
    {
        GitDiffChunker.Split(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Split_WhenTextHasNoFileHeader_ShouldIgnoreIt()
    {
        GitDiffChunker.Split("warning: something happened\nsecond line").Should().BeEmpty();
    }

    [Fact]
    public void Split_WhenDeletedFile_ShouldReportDeletedKind()
    {
        const string diff =
            """
            diff --git a/src/Gone.cs b/src/Gone.cs
            deleted file mode 100644
            --- a/src/Gone.cs
            +++ /dev/null
            @@ -1 +0,0 @@
            -public class Gone;
            """;

        var chunk = GitDiffChunker.Split(diff).Should().ContainSingle().Subject;

        chunk.Kind.Should().Be(ChangeKind.Deleted);
        chunk.DeletedLines.Should().Be(1);
    }

    [Fact]
    public void Split_ShouldEstimateNoTokensUntilLimitsAreApplied()
    {
        var chunk = GitDiffChunker.Split(TwoFileDiff)[0];

        chunk.EstimatedTokens.Should().Be(0);
        chunk.IsSkipped.Should().BeFalse();
        chunk.SkipReason.Should().BeNull();
    }

    [Fact]
    public void Split_WithNullArgument_ShouldThrow()
    {
        var act = () => GitDiffChunker.Split(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
