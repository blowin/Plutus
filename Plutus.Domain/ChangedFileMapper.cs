using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diffing;

namespace Plutus.Domain;

/// <summary>
/// Translates the repository-relative paths reported by git into paths visible from each bundle root.
/// Diff filtering, the directory tree and the raw diff section all depend on this single translation, so
/// a bundle rooted at a subdirectory reports exactly the changes beneath it.
/// </summary>
public sealed class ChangedFileMapper(string repositoryRoot, bool ignorePathCase = false)
{
    /// <summary>
    /// Groups the repository-relative diff by the bundle root each file belongs to and drops files that
    /// fall outside every root.
    /// </summary>
    public GitDiffContext Resolve(GitDiffContext diff, IReadOnlyList<IFileInfo> roots)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(roots);

        var groups = new List<DiffFileGroup>(roots.Count);
        foreach (var root in roots)
        {
            var rootPath = string.IsNullOrEmpty(root.PhysicalPath) ? root.Name : root.PhysicalPath;
            groups.Add(new DiffFileGroup(rootPath, Relativize(diff.Files, rootPath), RelativizePaths(diff.UntrackedFiles, rootPath)));
        }

        // The flat lists are the union of the groups, so both views always agree.
        return diff with
        {
            Files = groups.SelectMany(group => group.Files).ToArray(),
            UntrackedFiles = groups.SelectMany(group => group.UntrackedPaths).ToArray(),
            FileGroups = groups
        };
    }

    /// <summary>Reports whether a scanned root is the repository itself or lives inside it.</summary>
    public bool IsInsideRepository(string rootPath)
    {
        var comparison = ignorePathCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootPrefix = VcsPathNormalizer.RootPrefix(rootPath);
        var repositoryPrefix = VcsPathNormalizer.RootPrefix(repositoryRoot);

        return repositoryPrefix.StartsWith(rootPrefix, comparison) || rootPrefix.StartsWith(repositoryPrefix, comparison);
    }

    /// <summary>
    /// Git reports paths relative to the repository root, so each path is anchored there first and the root
    /// prefix is stripped. A root that is the repository itself keeps the path unchanged, a nested root
    /// removes only its own prefix, and a path outside the root is dropped.
    /// </summary>
    private IReadOnlyList<ChangedFile> Relativize(IReadOnlyList<ChangedFile> files, string rootPath)
    {
        var relative = new List<ChangedFile>(files.Count);
        foreach (var file in files)
        {
            if (TryRelativize(file.Path, rootPath, out var relativePath))
            {
                relative.Add(file with { Path = relativePath });
            }
        }

        return relative;
    }

    private IReadOnlyList<string> RelativizePaths(IReadOnlyList<string> paths, string rootPath)
    {
        var relative = new List<string>(paths.Count);
        foreach (var path in paths)
        {
            if (TryRelativize(path, rootPath, out var relativePath))
            {
                relative.Add(relativePath);
            }
        }

        return relative;
    }

    private bool TryRelativize(string repositoryRelativePath, string rootPath, out string relativePath)
    {
        relativePath = string.Empty;

        var comparison = ignorePathCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootPrefix = VcsPathNormalizer.RootPrefix(rootPath);
        var anchored = VcsPathNormalizer.Anchor(repositoryRoot, repositoryRelativePath);
        if (!anchored.StartsWith(rootPrefix, comparison))
        {
            return false;
        }

        // The prefix is used only for matching; slicing the original keeps the path casing intact.
        relativePath = anchored[rootPrefix.Length..];
        return true;
    }
}
