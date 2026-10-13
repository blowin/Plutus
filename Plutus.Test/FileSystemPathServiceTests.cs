using FluentAssertions;
using Plutus.Infrastructure;

namespace Plutus.Test;

public class FileSystemPathServiceTests
{
    private readonly FileSystemPathService _service = new();

    [Fact]
    public void GetExpandPath_ShouldReturnAbsolutePath()
    {
        var result = _service.GetExpandPath(".");

        Path.IsPathFullyQualified(result).Should().BeTrue();
        result.Should().Be(Path.GetFullPath("."));
    }

    [Fact]
    public void GetExpandPath_ShouldExpandHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _service.GetExpandPath("~/projects/plutus")
            .Should().Be(Path.GetFullPath(Path.Combine(home, "projects", "plutus")));
    }

    [Fact]
    public void GetExpandPath_ShouldExpandEnvironmentVariables()
    {
        const string variableName = "PLUTUS_TEST_PATH";
        var previous = Environment.GetEnvironmentVariable(variableName);
        var expected = Path.Combine(Path.GetTempPath(), "plutus-path-test");
        Environment.SetEnvironmentVariable(variableName, expected);

        try
        {
            var variableReference = OperatingSystem.IsWindows()
                ? $"%{variableName}%"
                : $"${variableName}";
            _service.GetExpandPath(variableReference)
                .Should().Be(Path.GetFullPath(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, previous);
        }
    }
}
