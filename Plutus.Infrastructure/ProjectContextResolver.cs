using Microsoft.Extensions.FileProviders;
using Plutus.Domain;

namespace Plutus.Infrastructure;

public class ProjectContextResolver(IFileInfoDetailProvider provider, IReadOnlyCollection<IRemoteRepository> remoteRepositories) : IProjectContextResolver
{
    public async Task<ProjectResolveResponse?> ResolveContextAsync(List<string>? projectPaths, string? outputPath)
    {
        if (projectPaths == null || projectPaths.Count == 0)
        {
            Console.Error.WriteLine("Error: No project paths specified.");
            return null;
        }

        var processedPaths = await GetPathForProcessAsync(projectPaths);
        var roots = processedPaths?.Select(ResolveDirectory).ToList();
        if (roots is null)
        {
            return null;
        }

        var primaryRoot = roots.First();
        var normalizeOutputPath = NormalizeOutputPath(projectPaths, outputPath);
        var output = ResolveOutput(primaryRoot, normalizeOutputPath);

        if (output.IsDirectory)
        {
            Console.Error.WriteLine($"Error: Output path is a directory: {output.PhysicalPath ?? output.Name}");
            return null;
        }

        return new(roots, output);
    }

    private async Task<List<string>?> GetPathForProcessAsync(List<string> projectPaths)
    {
        var processedPaths = new List<string>();
        foreach (var path in projectPaths)
        {
            try
            {
                var remotePath = await ExtractRemotePathAsync(path);
                processedPaths.Add(remotePath ?? path);
            }
            catch
            {
                return null;
            }
        }

        return processedPaths;

        async ValueTask<string?> ExtractRemotePathAsync(string path)
        {
            foreach (var remoteRepositoryProvider in remoteRepositories)
            {
                if (!remoteRepositoryProvider.IsSupportedPath(path))
                {
                    continue;
                }

                Console.WriteLine($"Downloading remote repository: {path}...");
                try
                {
                    return await remoteRepositoryProvider.DownloadAsync(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error downloading repository {path}: {ex.Message}");
                    throw;
                }
            }

            return null;
        }
    }

    private string? NormalizeOutputPath(List<string> projectPaths, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return outputPath;
        }

        var firstProjectPath = projectPaths.First();
        return remoteRepositories
            .Where(r => r.IsSupportedPath(firstProjectPath))
            .Select(r => r.NormalizeOutputPath(firstProjectPath))
            .FirstOrDefault();
    }

    private IFileInfo ResolveDirectory(string path)
    {
        var full = path.GetExpandPath();
        var dir = provider.CreateDirectory(full);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Project directory not found: {full}");
        }

        return dir;
    }

    private IFileInfo ResolveOutput(IFileInfo root, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return provider.CreateFile(outputPath.GetExpandPath());
        }

        var folderName = string.IsNullOrWhiteSpace(root.Name) ? "project" : root.Name;
        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), $"{folderName}_bundle.md");
        return provider.CreateFile(defaultPath.GetExpandPath());
    }
}
