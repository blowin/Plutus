using FluentAssertions;
using Plutus.Domain.Diffing;

namespace Plutus.Test.Diffing;

public class GitDiffParserTests
{
    private const string MultiFileDiff =
        """
        diff --git a/src/Core.cs b/src/Core.cs
        index 1111111..2222222 100644
        --- a/src/Core.cs
        +++ b/src/Core.cs
        @@ -1,3 +1,3 @@
        -var old = 1;
        +var updated = 2;
        diff --git a/src/New.cs b/src/New.cs
        new file mode 100644
        index 0000000..3333333
        --- /dev/null
        +++ b/src/New.cs
        @@ -0,0 +1 @@
        +public class New;
        """;

    [Fact]
    public void ParseChangedFiles_WithModifiedAndAddedFiles_ShouldReportBothWithKinds()
    {
        var files = GitDiffParser.ParseChangedFiles(MultiFileDiff);

        files.Should().HaveCount(2);
        files[0].Should().Be(new ChangedFile("src/Core.cs", ChangeKind.Modified));
        files[1].Should().Be(new ChangedFile("src/New.cs", ChangeKind.Added));
    }

    [Fact]
    public void ParseChangedFiles_WhenFileWasDeleted_ShouldReportDeletedKind()
    {
        const string diff =
            """
            diff --git a/src/Gone.cs b/src/Gone.cs
            deleted file mode 100644
            index 4444444..0000000
            --- a/src/Gone.cs
            +++ /dev/null
            @@ -1 +0,0 @@
            -public class Gone;
            """;

        GitDiffParser.ParseChangedFiles(diff)
            .Should().ContainSingle()
            .Which.Should().Be(new ChangedFile("src/Gone.cs", ChangeKind.Deleted));
    }

    [Fact]
    public void ParseChangedFiles_WhenPathContainsSpaces_ShouldKeepWholePath()
    {
        const string diff =
            """
            diff --git a/docs/Release Notes.md b/docs/Release Notes.md
            index 1111111..2222222 100644
            --- a/docs/Release Notes.md
            +++ b/docs/Release Notes.md
            @@ -1 +1 @@
            -old
            +new
            """;

        GitDiffParser.ParseChangedFiles(diff)
            .Should().ContainSingle()
            .Which.Path.Should().Be("docs/Release Notes.md");
    }

    [Fact]
    public void ParseChangedFiles_WithRenameHeader_ShouldKeepDestinationPathAndRenameKind()
    {
        const string diff =
            """
            diff --git a/src/Old.cs b/src/New.cs
            similarity index 95%
            rename from src/Old.cs
            rename to src/New.cs
            """;

        GitDiffParser.ParseChangedFiles(diff)
            .Should().ContainSingle()
            .Which.Should().Be(new ChangedFile("src/New.cs", ChangeKind.Renamed));
    }

    [Fact]
    public void ParseChangedFiles_WithModeOnlyChange_ShouldReportTypeChanged()
    {
        const string diff =
            """
            diff --git a/scripts/tool.sh b/scripts/tool.sh
            old mode 100644
            new mode 100755
            """;

        GitDiffParser.ParseChangedFiles(diff)
            .Should().ContainSingle()
            .Which.Should().Be(new ChangedFile("scripts/tool.sh", ChangeKind.TypeChanged));
    }

    [Fact]
    public void ParseChangedFiles_WhenHunkBodyMentionsDiffHeaders_ShouldNotTreatThemAsFiles()
    {
        const string diff =
            """
            diff --git a/README.md b/README.md
            index 1111111..2222222 100644
            --- a/README.md
            +++ b/README.md
            @@ -1,2 +1,3 @@
             A line describing the format:
            +  diff --git a/fake.cs b/fake.cs
            +  new file mode 100644
            """;

        GitDiffParser.ParseChangedFiles(diff)
            .Should().ContainSingle()
            .Which.Should().Be(new ChangedFile("README.md", ChangeKind.Modified));
    }

    [Fact]
    public void ParseChangedFiles_WhenDiffIsEmpty_ShouldReturnNoFiles()
    {
        GitDiffParser.ParseChangedFiles(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void ParseChangedFiles_WhenHeaderUsesBarePaths_ShouldStillReportTheDestination()
    {
        const string diff =
            """
            diff --git src/Plain.cs src/Plain.cs
            index 1111111..2222222 100644
            --- src/Plain.cs
            +++ src/Plain.cs
            """;

        GitDiffParser.ParseChangedFiles(diff)
            .Should().ContainSingle()
            .Which.Path.Should().Be("src/Plain.cs");
    }

    [Fact]
    public void ParseChangedFiles_WhenTextContainsNoDiffHeaders_ShouldReturnNoFiles()
    {
        const string diff =
            """
            This is a commit message, not a diff.
            It mentions --- and +++ but no file header.
            """;

        GitDiffParser.ParseChangedFiles(diff).Should().BeEmpty();
    }

    [Fact]
    public void ParseChangedFiles_WhenGitEmitsABinaryPayload_ShouldStillReportThePath()
    {
        const string diff =
            """
            diff --git a/assets/logo.png b/assets/logo.png
            index 1111111..2222222 100644
            GIT binary patch
            literal 12
            TcmZQzU|?VQ0000
            """;

        GitDiffParser.ParseChangedFiles(diff)
            .Should().ContainSingle()
            .Which.Path.Should().Be("assets/logo.png");
    }
}
