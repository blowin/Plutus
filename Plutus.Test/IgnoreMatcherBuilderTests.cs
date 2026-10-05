using FluentAssertions;
using NSubstitute;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class IgnoreMatcherBuilderTests
{
    [Fact]
    public void Build_WithNoMatchers_ShouldReturnWorkingMatcher()
    {
        // Arrange
        var builder = new IgnoreMatcherBuilder();

        // Act
        var matcher = builder.Build();

        // Assert
        matcher.IsIgnoredFile("src/Program.cs").Should().BeFalse();
        matcher.IsIgnoredDirectory("src/").Should().BeFalse();
    }

    [Fact]
    public void Build_WithMultipleMatchers_ShouldCombineTheirLogic()
    {
        // Arrange
        var mockMatcher1 = new TestMatcher().IgnoreFile("file1.txt");
        var mockMatcher2 = new TestMatcher().IgnoreFile("file2.txt");

        var matcher = new IgnoreMatcherBuilder()
            .Add(mockMatcher1)
            .Add(mockMatcher2)
            .Build();

        // Act & Assert
        matcher.IsIgnoredFile("file1.txt").Should().BeTrue();
        matcher.IsIgnoredFile("file2.txt").Should().BeTrue();
        matcher.IsIgnoredFile("safe.txt").Should().BeFalse();
    }

    private sealed class TestMatcher : IIgnoreMatcher
    {
        private readonly List<string> _ignoreFiles = [];

        public bool IsIgnoredFile(string relativePath) => _ignoreFiles.Contains(relativePath);

        public bool IsIgnoredDirectory(string relativePath) => false;

        public TestMatcher IgnoreFile(string relativePath)
        {
            _ignoreFiles.Add(relativePath);
            return this;
        }
    }
}
