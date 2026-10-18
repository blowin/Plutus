using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diagnostics;

namespace Plutus.Domain.Dependencies;

public interface IDependencyResolver
{
    Task<IReadOnlyCollection<ResolvedFile>> ResolveAsync(IReadOnlyCollection<string> initialFiles, IFileProvider fileProvider, CancellationToken ct);
}

public record ResolvedFile(string VirtualPath, bool IsDirect, IReadOnlyCollection<string> InclusionReasons);

public sealed class DependencyResolver(
    IEnumerable<IDependencyAnalyzer> analyzers,
    IRunOutput output) : IDependencyResolver
{
    public async Task<IReadOnlyCollection<ResolvedFile>> ResolveAsync(
        IReadOnlyCollection<string> initialFiles,
        IFileProvider fileProvider,
        CancellationToken ct)
    {
        var visited = new Dictionary<string, ResolvedFileBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in initialFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            visited[file] = new ResolvedFileBuilder(file, isDirect: true);
        }

        var queue = new Queue<string>(visited.Keys);

        while (queue.Count > 0)
        {
            var currentPath = queue.Dequeue();
            var analyzer = analyzers.FirstOrDefault(a => a.CanAnalyze(currentPath));

            if (analyzer is null)
            {
                continue;
            }

            var result = await analyzer.AnalyzeAsync(currentPath, fileProvider, ct);
            if (result is null)
            {
                continue;
            }

            foreach (var warning in result.Warnings)
            {
                output.Warning(warning);
            }

            foreach (var dep in result.Dependencies.OrderBy(d => d.RawReference))
            {
                if (dep.ResolvedVirtualPath is null)
                {
                    output.Warning($"Unresolved reference '{dep.RawReference}' in '{currentPath}'. Skipping.");
                    continue;
                }

                if (!visited.TryGetValue(dep.ResolvedVirtualPath, out var builder))
                {
                    builder = new ResolvedFileBuilder(dep.ResolvedVirtualPath, isDirect: false);
                    visited[dep.ResolvedVirtualPath] = builder;
                    queue.Enqueue(dep.ResolvedVirtualPath);
                }

                builder.Reasons.Add($"{currentPath} -> {dep.Reason}");
            }
        }

        return visited.Values.Select(b => b.Build()).OrderBy(f => f.VirtualPath).ToList();
    }

    private sealed class ResolvedFileBuilder(string path, bool isDirect)
    {
        public List<string> Reasons { get; } = [];
        public ResolvedFile Build() => new(path, isDirect, Reasons.AsReadOnly());
    }
}
