using System.IO.Compression;
using System.Net;
using FluentAssertions;
using Plutus.Infrastructure.RemoteRepository;

namespace Plutus.Test.RemoteRepository;

public class RemoteRepositoryTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo", null, null, "https://github.com/owner/repo/archive/master.zip")]
    [InlineData("https://github.com/owner/repo/tree/main", "main", null, "https://github.com/owner/repo/archive/main.zip")]
    [InlineData("https://github.com/owner/repo", "feature/my-branch", null, "https://github.com/owner/repo/archive/feature/my-branch.zip")]
    [InlineData("https://github.com/owner/repo", null, "a1b2c3d", "https://github.com/owner/repo/archive/a1b2c3d.zip")]
    [InlineData("https://github.com/owner/repo.git", "v1.2.0", null, "https://github.com/owner/repo/archive/v1.2.0.zip")]
    public async Task GitHub_ShouldBuildArchiveUrl(string path, string? branch, string? commit, string expectedUrl)
    {
        using var handler = new ArchiveHttpHandler();
        using var client = new HttpClient(handler);
        using var repository = new GitHubRemoteRepository(client);

        var downloadedPath = await repository.DownloadAsync(path, branch, commit);

        handler.RequestedUri.Should().Be(expectedUrl);
        Directory.Exists(downloadedPath).Should().BeTrue();
        File.Exists(Path.Combine(downloadedPath, "README.md")).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://gitlab.com/group/repo", null, null, "https://gitlab.com/api/v4/projects/group%2Frepo/repository/archive.zip")]
    [InlineData("https://gitlab.com/group/subgroup/repo/-/tree/main", "main", null, "https://gitlab.com/api/v4/projects/group%2Fsubgroup%2Frepo/repository/archive.zip?sha=main")]
    [InlineData("https://gitlab.com/group/repo", "feature/my-branch", null, "https://gitlab.com/api/v4/projects/group%2Frepo/repository/archive.zip?sha=feature%2Fmy-branch")]
    [InlineData("https://gitlab.com/group/repo", null, "a1b2c3d", "https://gitlab.com/api/v4/projects/group%2Frepo/repository/archive.zip?sha=a1b2c3d")]
    public async Task GitLab_ShouldBuildArchiveUrl(string path, string? branch, string? commit, string expectedUrl)
    {
        using var handler = new ArchiveHttpHandler();
        using var client = new HttpClient(handler);
        using var repository = new GitLabRemoteRepository(client);

        await repository.DownloadAsync(path, branch, commit);

        handler.RequestedUri.Should().Be(expectedUrl);
    }

    [Theory]
    [InlineData("https://bitbucket.org/workspace/repo", null, null, "https://bitbucket.org/workspace/repo/get/master.zip")]
    [InlineData("https://bitbucket.org/workspace/repo/src/main", "feature/my-branch", null, "https://bitbucket.org/workspace/repo/get/feature/my-branch.zip")]
    [InlineData("https://bitbucket.org/workspace/repo", null, "a1b2c3d", "https://bitbucket.org/workspace/repo/get/a1b2c3d.zip")]
    public async Task Bitbucket_ShouldBuildArchiveUrl(string path, string? branch, string? commit, string expectedUrl)
    {
        using var handler = new ArchiveHttpHandler();
        using var client = new HttpClient(handler);
        using var repository = new BitbucketRemoteRepository(client);

        await repository.DownloadAsync(path, branch, commit);

        handler.RequestedUri.Should().Be(expectedUrl);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo", true)]
    [InlineData("http://github.com/owner/repo", true)]
    [InlineData("https://github.com.evil.example/owner/repo", false)]
    [InlineData("https://gitlab.com/owner/repo", false)]
    public void GitHub_IsSupportedPath_ShouldRequireExactHost(string path, bool expected)
    {
        using var client = new HttpClient();
        using var repository = new GitHubRemoteRepository(client);

        repository.IsSupportedPath(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://gitlab.com/group/repo", true)]
    [InlineData("https://gitlab.com.evil.example/group/repo", false)]
    [InlineData("file:///group/repo", false)]
    public void GitLab_IsSupportedPath_ShouldRequireExactHost(string path, bool expected)
    {
        using var client = new HttpClient();
        using var repository = new GitLabRemoteRepository(client);

        repository.IsSupportedPath(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://bitbucket.org/workspace/repo", true)]
    [InlineData("https://bitbucket.org.attacker.test/workspace/repo", false)]
    [InlineData("C:\\repo", false)]
    public void Bitbucket_IsSupportedPath_ShouldRequireExactHost(string path, bool expected)
    {
        using var client = new HttpClient();
        using var repository = new BitbucketRemoteRepository(client);

        repository.IsSupportedPath(path).Should().Be(expected);
    }

    [Fact]
    public async Task DownloadAsync_WhenBranchAndCommitProvided_ShouldThrowWithoutRequest()
    {
        using var handler = new ArchiveHttpHandler();
        using var client = new HttpClient(handler);
        using var repository = new GitHubRemoteRepository(client);

        Func<Task> act = async () => await repository.DownloadAsync("https://github.com/owner/repo", "main", "a1b2c3d");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*either a branch/tag or a commit*");
        handler.RequestedUri.Should().BeNull();
    }

    [Theory]
    [InlineData("https://github.com")]
    [InlineData("https://github.com/owner")]
    [InlineData("file:///owner/repo")]
    public async Task GitHub_DownloadAsync_WhenRepositoryUrlIsInvalid_ShouldThrow(string path)
    {
        using var client = new HttpClient(new ArchiveHttpHandler());
        using var repository = new GitHubRemoteRepository(client);

        Func<Task> act = async () => await repository.DownloadAsync(path);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class ArchiveHttpHandler : HttpMessageHandler
    {
        public string? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri?.AbsoluteUri;
            var content = new ByteArrayContent(CreateArchive());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }

        private static byte[] CreateArchive()
        {
            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("owner-repo/README.md");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("test archive");
            }
            return memory.ToArray();
        }
    }
}
