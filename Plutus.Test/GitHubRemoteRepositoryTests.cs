using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.ArchiveExtractor;
using Plutus.Infrastructure.RemoteRepository;
using Plutus.Test.RemoteRepository;

namespace Plutus.Test;

public class GitHubRemoteRepositoryTests
{
    private static readonly IFileInfoDetailProvider MockProvider = new MockFileInfoDetailProvider();

    private class TestGitHubRepository(HttpClient client) : GitHubRemoteRepository(client, new SafeArchiveExtractor(MockProvider, SafeExtractionOptions.Default))
    {
        public string ExposeBuildZipUrl(string path) => BuildZipUrl(path, null, null);
    }

    [Theory]
    [InlineData("https://github.com", true)]
    [InlineData("http://github.com", true)]
    [InlineData("https://gitlab.com", false)]
    [InlineData("C:\\LocalPath", false)]
    public void IsSupportedPath_ShouldEvaluateCorrectly(string path, bool expected)
    {
        // Arrange
        using var client = new HttpClient();
        using var repo = new GitHubRemoteRepository(client, new SafeArchiveExtractor(MockProvider, SafeExtractionOptions.Default));

        // Act
        var result = repo.IsSupportedPath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void BuildZipUrl_ShouldFormCorrectGitHubZipEndpoint()
    {
        // Arrange
        using var client = new HttpClient();
        using var repo = new TestGitHubRepository(client);
        var repoUrl = "https://github.com/user/project";

        // Act
        var zipUrl = repo.ExposeBuildZipUrl(repoUrl);

        // Assert
        zipUrl.Should().Be("https://github.com/user/project/archive/master.zip");
    }
}
