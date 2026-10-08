using FluentAssertions;
using MAB.DotIgnore;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.Test;

public class GitIgnoreMatcherTests
{
    [Theory]
    [InlineData("frontend/", "frontend", true)]
    [InlineData("frontend/", "src/frontend", true)]
    [InlineData("frontend/", "src/sub/frontend", true)]
    [InlineData("frontend", "src/frontend", true)]
    [InlineData("/frontend/", "frontend", true)]
    [InlineData("/frontend/", "src/frontend", false)]
    [InlineData("*/frontend", "src/frontend", true)]
    public void IsIgnoredDirectory_ShouldMatchNestedDirectories(string pattern, string path, bool expected)
    {
        // Arrange
        var ignoreList = new IgnoreList(new[] { pattern });
        var matcher = new GitIgnoreMatcher(ignoreList);

        // Act
        var result = matcher.IsIgnoredDirectory(path);

        // Assert
        result.Should().Be(expected);
    }

     #region Exclude File/Directory Matcher Tests

    [Theory]
    [InlineData("*.log", "logs/app.log", true)]
    [InlineData("*.log", "src/Program.cs", false)]
    [InlineData("secret.txt", "config/secret.txt", true)]
    public void FromExcludeFiles_ShouldOnlyIgnoreMatchingFiles(string pattern, string filePath, bool expectedValue)
    {
        // Arrange
        var matcher = GitIgnoreMatcher.FromExcludeFiles([pattern]);

        // Act
        var result = matcher.IsIgnoredFile(filePath);

        // Assert
        result.Should().Be(expectedValue);
        matcher.IsIgnoredDirectory("any/dir/").Should().BeFalse(); // Dirs must never be ignored here
    }

    [Theory]
    [InlineData("bin/", "bin", true)]
    [InlineData("bin/", "src/bin", true)]
    [InlineData("bin/", "src/Program.cs", false)]
    public void FromExcludeDirs_ShouldOnlyIgnoreMatchingDirectories(string pattern, string dirPath, bool expectedValue)
    {
        // Arrange
        var matcher = GitIgnoreMatcher.FromExcludeDirs([pattern]);

        // Act
        var result = matcher.IsIgnoredDirectory(dirPath);

        // Assert
        result.Should().Be(expectedValue);
        matcher.IsIgnoredFile("any/file.txt").Should().BeFalse(); // Files must never be ignored here
    }

    #endregion

    #region Include File Matcher Tests

    [Theory]
    [InlineData("*.cs", "src/Program.cs", false)]       // Matches whitelist -> DO NOT IGNORE
    [InlineData("*.cs", "assets/image.png", true)]      // Fails whitelist -> IGNORE
    [InlineData("src/App.cs", "src/App.cs", false)]     // Matches strict whitelist -> DO NOT IGNORE
    [InlineData("src/App.cs", "src/Domain.cs", true)]   // Fails strict whitelist -> IGNORE
    public void FromIncludeFiles_ShouldIgnoreEverythingExceptWhitelistedFiles(string pattern, string filePath, bool expectedValue)
    {
        // Arrange
        var matcher = GitIgnoreMatcher.FromIncludeFiles([pattern]);

        // Act
        var result = matcher.IsIgnoredFile(filePath);

        // Assert
        result.Should().Be(expectedValue);
    }

    #endregion

    #region Include Directory Matcher & Lookahead Tests

    [Fact]
    public void FromIncludeDirs_WithDeepTarget_ShouldAllowParentTraversalButPruneUnrelatedBranches()
    {
        // Arrange: User strictly wants to include files inside "src/Core"
        var matcher = GitIgnoreMatcher.FromIncludeDirs(["src/Core/"]);

        // Act & Assert Case 1: Checking root-level "src" folder
        // Lookahead check must notice that "src" is a parent route to "src/Core" -> DO NOT IGNORE
        matcher.IsIgnoredDirectory("src").Should().BeFalse();
        matcher.IsIgnoredDirectory("src/").Should().BeFalse();

        // Act & Assert Case 2: Checking exact matching target directory -> DO NOT IGNORE
        matcher.IsIgnoredDirectory("src/Core").Should().BeFalse();

        // Act & Assert Case 3: Checking sibling or completely unrelated folder tracks -> IGNORE
        matcher.IsIgnoredDirectory("frontend").Should().BeTrue();
        matcher.IsIgnoredDirectory("src/Tests").Should().BeTrue();
    }

    [Theory]
    [InlineData("src/", "src/Program.cs", false)]        // Inside a whitelisted folder path -> DO NOT IGNORE
    [InlineData("src/Core/", "README.md", true)]         // In root, outside whitelisted folder track -> IGNORE
    [InlineData("src/Core/", "src/App.cs", true)]        // Inside "src", but outside "src/Core" target -> IGNORE
    [InlineData("src/Core/", "src/Core/App.cs", false)]  // Perfectly fits whitelisted location context -> DO NOT IGNORE
    public void FromIncludeDirs_ShouldPropagatePruningToFilesOutsideWhitelistedDirectories(string pattern, string filePath, bool expectedValue)
    {
        // Arrange
        var matcher = GitIgnoreMatcher.FromIncludeDirs([pattern]);

        // Act
        var result = matcher.IsIgnoredFile(filePath);

        // Assert
        result.Should().Be(expectedValue);
    }

    #endregion

    #region Empty Array & Whitespace Edge Cases

    [Fact]
    public void CreateIgnoreMatcherOrEmpty_WithEmptyCollections_ShouldReturnEmptyIgnoreMatcherInstance()
    {
        // Act
        var fileMatcher = GitIgnoreMatcher.FromIncludeFiles([]);
        var dirMatcher = GitIgnoreMatcher.FromExcludeDirs(["   ", ""]);

        // Assert
        fileMatcher.Should().Be(EmptyIgnoreMatcher.Instance);
        dirMatcher.Should().Be(EmptyIgnoreMatcher.Instance);

        // Assert that fallback behavior works and skips restrictions completely
        fileMatcher.IsIgnoredFile("any/file.cs").Should().BeFalse();
        dirMatcher.IsIgnoredDirectory("any/dir").Should().BeFalse();
    }

    #endregion

    [Fact]
    public void FromIncludeDirs_WithComplexVaryingNestingSlaches_ShouldNormalizeLookaheadRoutes()
    {
        // Scenario A: Whitelist target pattern does NOT have a trailing slash
        var matcherWithoutSlash = GitIgnoreMatcher.FromIncludeDirs(["src/Modules/Database"]);

        // Traversal path must still resolve correctly
        matcherWithoutSlash.IsIgnoredDirectory("src").Should().BeFalse();
        matcherWithoutSlash.IsIgnoredDirectory("src/Modules").Should().BeFalse();
        matcherWithoutSlash.IsIgnoredDirectory("src/Modules/Database").Should().BeFalse();

        // Scenario B: Whitelist target pattern DOES have a trailing slash
        var matcherWithSlash = GitIgnoreMatcher.FromIncludeDirs(["src/Modules/Network/"]);

        matcherWithSlash.IsIgnoredDirectory("src").Should().BeFalse();
        matcherWithSlash.IsIgnoredDirectory("src/Modules").Should().BeFalse();
        matcherWithSlash.IsIgnoredDirectory("src/Modules/Network").Should().BeFalse();
    }

    [Fact]
    public void FromIncludeDirs_WithPartialNameMatches_ShouldNotAccidentallyTraverseSimilarlyNamedDirectories()
    {
        // Arrange: User ONLY wants "src/Core-v2"
        // A naive string .StartsWith() lookup could mistakenly allow "src/Core" to be entered
        var matcher = GitIgnoreMatcher.FromIncludeDirs(["src/Core-v2/"]);

        // Act & Assert
        matcher.IsIgnoredDirectory("src").Should().BeFalse(); // Valid structural path parent step

        // This directory should be PRUNED entirely because it's an unrelated track,
        // despite starting with a similar string layout ("src/Core" vs "src/Core-v2")
        matcher.IsIgnoredDirectory("src/Core").Should().BeTrue();

        // The real target is clear to run
        matcher.IsIgnoredDirectory("src/Core-v2").Should().BeFalse();
    }

    [Fact]
    public void FromIncludeDirs_WithDeeplyNestedFilePruning_ShouldEvaluateMultiLayerFileLeaking()
    {
        // Arrange: User isolates deep segment feature tracking
        var matcher = GitIgnoreMatcher.FromIncludeDirs(["src/Features/Billing/Core/"]);

        // Any files encountered during structural navigation of parent levels should be pruned
        matcher.IsIgnoredFile("src/root_level_leaker.cs").Should().BeTrue();
        matcher.IsIgnoredFile("src/Features/feature_level_leaker.cs").Should().BeTrue();
        matcher.IsIgnoredFile("src/Features/Billing/billing_level_leaker.cs").Should().BeTrue();

        // Codepayload located inside the correct destination must be parsed cleanly
        matcher.IsIgnoredFile("src/Features/Billing/Core/InvoiceService.cs").Should().BeFalse();
    }
}
