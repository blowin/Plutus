using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.Diagnostics;

namespace Plutus.Infrastructure;

public class ProjectContextResolver(
    IFileInfoDetailProvider provider,
    IReadOnlyCollection<IRemoteRepository> remoteRepositories,
    IPathService pathService,
    IRunOutput? runOutput = null) : IProjectContextResolver
{
    private readonly IRunOutput _output = runOutput ?? NullRunOutput.Instance;

    public async Task<ProjectResolveResponse?> ResolveContextAsync(List<string>? projectPaths, string? outputPath, string? format, string? branch, string? commit, CancellationToken cancellationToken = default)
    {
        if (projectPaths == null || projectPaths.Count == 0)
        {
            _output.Error("No project paths specified.");
            return null;
        }

        var rootDetails = await ResolveRoots(projectPaths, branch, commit, cancellationToken);
        if (rootDetails is null)
        {
            return null;
        }

        var primaryRoot = rootDetails.Value.Roots.First();
        var normalizeOutputPath = NormalizeOutputPath(projectPaths, outputPath);
        var output = ResolveOutput(primaryRoot, normalizeOutputPath, format);

        if (output.IsDirectory)
        {
            _output.Error($"Output path is a directory: {output.PhysicalPath ?? output.Name}");
            return null;
        }

        return new(rootDetails.Value.Roots, output, rootDetails.Value.ExplicitFiles);
    }

    private async Task<(List<IFileInfo> Roots, List<string> ExplicitFiles)?> ResolveRoots(List<string> projectPaths, string? branch, string? commit, CancellationToken cancellationToken)
    {
        var processedPaths = await GetPathForProcessAsync(projectPaths, branch, commit, cancellationToken);
        var roots = processedPaths?.Select(e => ResolveDirectory(e.Directory)).ToList();
        if (roots is null)
        {
            return null;
        }

        return (roots, []);
    }

    private async Task<List<(string Directory, string? ExplicitFile)>?> GetPathForProcessAsync(List<string> projectPaths, string? branch, string? commit, CancellationToken cancellationToken)
    {
        var processedPaths = new List<(string Directory, string? ExplicitFile)>();
        foreach (var path in projectPaths)
        {
            try
            {
                var remotePath = await ExtractRemotePathAsync(path);
                if (remotePath is not null)
                {
                    processedPaths.Add((remotePath, null));
                }
                else
                {
                    if (provider.IsFile(path))
                    {
                        var fileInfo = provider.CreateFile(path);
                        var dir = Path.GetDirectoryName(fileInfo.PhysicalPath ?? path) ?? throw new DirectoryNotFoundException($"Directory not found for file {path}");
                        processedPaths.Add((dir, path));
                    }
                    else
                    {
                        processedPaths.Add((path, null));
                    }
                }
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

                _output.Info($"Downloading remote repository: {path}...");
                try
                {
                    return await remoteRepositoryProvider.DownloadAsync(path, branch, commit, cancellationToken);
                }
                catch (Exception ex)
                {
                    _output.Error($"downloading repository {path}: {ex.Message}");
                    throw;
                }
            }

            if (!string.IsNullOrWhiteSpace(branch) || !string.IsNullOrWhiteSpace(commit))
            {
                throw new ArgumentException("Branch and commit can only be used with remote repository URLs.", nameof(projectPaths));
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
        var full = pathService.GetExpandPath(path);
        var dir = provider.CreateDirectory(full);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Project directory not found: {full}");
        }

        return dir;
    }

    private IFileInfo ResolveOutput(IFileInfo root, string? outputPath, string? format)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return provider.CreateFile(pathService.GetExpandPath(outputPath));
        }

        var cleanFolderName = string.IsNullOrWhiteSpace(root.Name)
            ? "project"
            : root.Name.TrimEnd('/', '\\');

        var fileExt = format?.ToLowerInvariant() ?? "md";
        var defaultPath = Path.Combine(provider.GetCurrentDirectory(), $"{cleanFolderName}_bundle.{fileExt}");
        return provider.CreateFile(pathService.GetExpandPath(defaultPath));
    }
}
