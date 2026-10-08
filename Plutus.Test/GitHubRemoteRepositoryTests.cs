using FluentAssertions;
using Plutus.Infrastructure.RemoteRepository;

namespace Plutus.Test;

public class GitHubRemoteRepositoryTests
{
    private class TestGitHubRepository : GitHubRemoteRepository
    {
        public string ExposeBuildZipUrl(string path) => BuildZipUrl(path);
    }

    [Theory]
    [InlineData("https://github.com", true)]
    [InlineData("http://github.com", true)]
    [InlineData("https://gitlab.com", false)]
    [InlineData("C:\\LocalPath", false)]
    public void IsSupportedPath_ShouldEvaluateCorrectly(string path, bool expected)
    {
        // Arrange
        using var repo = new GitHubRemoteRepository();

        // Act
        var result = repo.IsSupportedPath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void BuildZipUrl_ShouldFormCorrectGitHubZipEndpoint()
    {
        // Arrange
        using var repo = new TestGitHubRepository();
        var repoUrl = "https://github.com";

        // Act
        var zipUrl = repo.ExposeBuildZipUrl(repoUrl);

        // Assert
        zipUrl.Should().Be("https://github.com/archive/refs/heads/master.zip");
    }
}
