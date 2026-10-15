using Microsoft.Extensions.FileProviders;
using Plutus.Domain;

namespace Plutus.Test;

/// <summary>
/// The committed files of this repository, read through <see cref="IFileProvider"/> exactly as the application reads
/// files. A test that verifies documentation or a real-world fixture needs the file that ships with the repository,
/// and this is the single place that knows where the repository root and the test output directory are.
/// </summary>
public static class TestFiles
{
    private static readonly string VolumeRoot = Path.GetPathRoot(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
    private static readonly FileProviderAccess Files = new(new PhysicalFileProvider(VolumeRoot), VolumeRoot);

    /// <summary>The repository root, found by walking up from the test output directory.</summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Reads a file of the repository, for example <c>README.md</c> or <c>docs/CLI.md</c>.</summary>
    public static string ReadFromRepository(string relativePath) =>
        Files.ReadAllText(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Reads a fixture the test project copies next to the tests.</summary>
    public static string ReadFixture(string relativePath) =>
        Files.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>True when a path exists, through the same access the tests read files with.</summary>
    public static bool Exists(string path) => Files.Exists(path);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Files.FileExists(Path.Combine(directory.FullName, "Plutus.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"The repository root could not be found above '{AppContext.BaseDirectory}'.");
    }
}
