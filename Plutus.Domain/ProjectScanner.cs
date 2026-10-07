using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class ProjectScanner(IFileInfoDetailProvider fileInfoDetailProvider, IIgnoreMatcher ignoreMatcher)
{
    public ProjectNode Scan(IFileProvider fileProvider, string rootName, string outputPhysicalPath, FileSize maxFileSize)
    {
        var rootNode = new ProjectNode
        {
            Name = rootName + "/",
            RelativePath = "",
            IsDirectory = true,
            Status = NodeStatus.Included
        };

        BuildTree(fileProvider, "", rootNode, outputPhysicalPath, maxFileSize);
        return rootNode;
    }

    private void BuildTree(IFileProvider fileProvider, string subPath, ProjectNode parentNode, string outputPhysicalPath, FileSize maxFileSize)
    {
        var contents = fileProvider.GetDirectoryContents(subPath);
        if (!contents.Exists)
        {
            return;
        }

        var children = contents.OrderBy(x => !x.IsDirectory)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var child in children)
        {
            // Используем ваши новые методы фильтрации: IsSamePath и IsReparsePoint
            if (child.IsSamePath(outputPhysicalPath) || fileInfoDetailProvider.IsReparsePoint(child))
            {
                continue;
            }

            var itemSubPath = string.IsNullOrEmpty(subPath) ? child.Name : $"{subPath}/{child.Name}";

            if (child.IsDirectory)
            {
                var isIgnored = ignoreMatcher.IsIgnoredDirectory(itemSubPath);
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
                    BuildTree(fileProvider, itemSubPath, childNode, outputPhysicalPath, maxFileSize);
                }
            }
            else
            {
                var fileStatus = NodeStatus.Included;
                if (ignoreMatcher.IsIgnoredFile(itemSubPath))
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
    OversizedFile
}

public class ProjectNode
{
    public required string Name { get; init; }
    public required string RelativePath { get; init; }
    public required bool IsDirectory { get; init; }
    public required NodeStatus Status { get; init; }
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
