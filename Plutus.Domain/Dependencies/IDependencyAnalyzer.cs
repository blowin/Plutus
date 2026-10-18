using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain.Dependencies;

public interface IDependencyAnalyzer
{
    bool CanAnalyze(string virtualPath);

    Task<DependencyAnalysisResult> AnalyzeAsync(string virtualPath, IFileProvider fileProvider, CancellationToken ct);
}

public record DependencyAnalysisResult(IReadOnlyCollection<DiscoveredDependency> Dependencies, IReadOnlyCollection<string> Warnings);

public record DiscoveredDependency(string RawReference, string? ResolvedVirtualPath, string Reason);
