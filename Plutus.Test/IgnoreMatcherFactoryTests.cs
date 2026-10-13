using FluentAssertions;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class IgnoreMatcherFactoryTests
{
    [Fact]
    public void CreateIgnoreMatcher_WhenDefaultsDisabledAndDangerousAllowed_ShouldOnlyUseAdditionalMatchers()
    {
        var matcher = new IgnoreMatcherFactory([new ExactFileMatcher("custom.txt")])
            .CreateIgnoreMatcher(useDefaultExcludes: false, allowDangerousFiles: true);

        matcher.IsIgnoredFile("custom.txt").Should().BeTrue();
        matcher.IsIgnoredFile("image.png").Should().BeFalse();
        matcher.IsIgnoredFile(".env").Should().BeFalse();
        matcher.IsIgnoredDirectory("node_modules").Should().BeFalse();
    }

    [Fact]
    public void CreateIgnoreMatcher_ShouldApplyDefaultJunkAndDangerousRules()
    {
        var matcher = new IgnoreMatcherFactory([])
            .CreateIgnoreMatcher(useDefaultExcludes: true, allowDangerousFiles: false);

        matcher.IsIgnoredFile("assets/image.png").Should().BeTrue();
        matcher.IsIgnoredDirectory("src/obj").Should().BeTrue();
        matcher.IsIgnoredFile("secrets/.env").Should().BeTrue();
        matcher.IsIgnoredFile(".env.example").Should().BeFalse();
        matcher.IsIgnoredFile("src/Program.cs").Should().BeFalse();
    }

    private sealed class ExactFileMatcher(string fileName) : IIgnoreMatcher
    {
        public bool IsIgnoredFile(string relativePath) =>
            Path.GetFileName(relativePath).Equals(fileName, StringComparison.OrdinalIgnoreCase);

        public bool IsIgnoredDirectory(string relativePath) => false;
    }
}
