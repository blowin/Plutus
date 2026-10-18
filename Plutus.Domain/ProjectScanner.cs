using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diffing;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class ProjectScanner(IFileInfoDetailProvider fileInfoDetailProvider, IIgnoreMatcher ignoreMatcher)
{
    /// <summary>
    /// When <paramref name="diffPaths"/> is supplied, the scan is restricted to it. A supplied but empty
    /// selection therefore includes nothing, which is what a diff with no changes has to mean.
    /// </summary>
    public ProjectNode Scan(IFileProvider fileProvider, string rootName, string outputPhysicalPath, FileSize maxFileSize, DiffPathSet? diffPaths = null)
    {
        var rootNode = new ProjectNode
        {
            Name = rootName + "/",
            RelativePath = "",
            IsDirectory = true,
            Status = diffPaths is null || diffPaths.Count > 0 ? NodeStatus.Included : NodeStatus.IgnoredDirectory
        };

        BuildTree(fileProvider, "", rootNode, outputPhysicalPath, maxFileSize, diffPaths);
        return rootNode;
    }

    private void BuildTree(IFileProvider fileProvider, string subPath, ProjectNode parentNode, string outputPhysicalPath, FileSize maxFileSize, DiffPathSet? diffPaths)
    {
        // An empty selection cannot admit anything, so the directory walk is skipped entirely.
        if (diffPaths is { Count: 0 })
        {
            return;
        }

        var contents = fileProvider.GetDirectoryContents(subPath);
        if (!contents.Exists)
        {
            return;
        }

        var children = contents.OrderBy(x => !x.IsDirectory)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var child in children)
        {
            if (child.IsSamePath(outputPhysicalPath) || fileInfoDetailProvider.IsReparsePoint(child))
            {
                continue;
            }

            var itemSubPath = string.IsNullOrEmpty(subPath) ? child.Name : $"{subPath}/{child.Name}";

            if (child.IsDirectory)
            {
                // A diff scoped run keeps only directories that still contain a changed file, so the tree
                // shows the surrounding structure without dragging in untouched modules.
                var isIgnored = ignoreMatcher.IsIgnoredDirectory(itemSubPath) ||
                                diffPaths is not null && !diffPaths.ContainsDirectory(itemSubPath);
                var childNode = new ProjectNode
                {
                    Name = child.Name,
                    RelativePath = itemSubPath,
                    IsDirectory = true,
                    Status = isIgnored ? NodeStatus.IgnoredDirectory : NodeStatus.Included
                };

                parentNode.Children.Add(childNode);

                if (!isIgnored)
                {
                    BuildTree(fileProvider, itemSubPath, childNode, outputPhysicalPath, maxFileSize, diffPaths);
                }
            }
            else
            {
                var fileStatus = NodeStatus.Included;
                if (ignoreMatcher.IsIgnoredFile(itemSubPath) || diffPaths is not null && !diffPaths.ContainsFile(itemSubPath))
                {
                    fileStatus = NodeStatus.IgnoredFile;
                }
                else if (child.Length > maxFileSize)
                {
                    fileStatus = NodeStatus.OversizedFile;
                }

                var childNode = new ProjectNode
                {
                    Name = child.Name,
                    RelativePath = itemSubPath,
                    IsDirectory = false,
                    Status = fileStatus,
                    FileInfo = child
                };

                parentNode.Children.Add(childNode);
            }
        }
    }
}

public enum NodeStatus
{
    Included,
    IgnoredDirectory,
    IgnoredFile,
    OversizedFile,
    SkippedFile,
    TokenBudgetOmitted
}

public class ProjectNode
{
    public required string Name { get; init; }
    public required string RelativePath { get; init; }
    public required bool IsDirectory { get; init; }
    public required NodeStatus Status { get; set; }
    public IFileInfo? FileInfo { get; init; } // NULL for directory
    public List<ProjectNode> Children { get; } = [];

    public List<BundleEntry> ExtractBundleEntries()
    {
        var entries = new List<BundleEntry>();
        CollectIncludedFiles(this, entries);
        return entries;
    }

    private static void CollectIncludedFiles(ProjectNode current, List<BundleEntry> entries)
    {
        if (current is { IsDirectory: false, Status: NodeStatus.Included, FileInfo: not null })
        {
            entries.Add(new BundleEntry(current.RelativePath, current.FileInfo));
        }

        foreach (var child in current.Children)
        {
            CollectIncludedFiles(child, entries);
        }
    }
}

public sealed record BundleEntry(string RelativePath, IFileInfo File);
