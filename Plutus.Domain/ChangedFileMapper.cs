using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diffing;

namespace Plutus.Domain;

/// <summary>
/// Translates the repository-relative paths reported by git into paths visible from each bundle root.
/// Diff filtering, the directory tree and the diff sections all depend on this single translation, so a
/// bundle rooted at a subdirectory reports only its own changes, with paths that match the rest of it.
/// </summary>
public sealed class ChangedFileMapper(string repositoryRoot, bool ignorePathCase = false)
{
    /// <summary>
    /// Groups the per-file diffs by the bundle root each one belongs to and drops chunks outside every root.
    /// Chunk paths and the paths inside the diff text are rewritten together so the two never disagree.
    /// </summary>
    public GitDiffContext Resolve(
        string comparisonBase,
        IReadOnlyList<DiffChunk> trackedChunks,
        IReadOnlyList<DiffChunk> untrackedChunks,
        IReadOnlyList<IFileInfo> roots)
    {
        ArgumentNullException.ThrowIfNull(comparisonBase);
        ArgumentNullException.ThrowIfNull(trackedChunks);
        ArgumentNullException.ThrowIfNull(untrackedChunks);
        ArgumentNullException.ThrowIfNull(roots);

        var groups = new List<DiffFileGroup>(roots.Count);
        foreach (var root in roots)
        {
            var rootPath = RootPath(root);
            groups.Add(new DiffFileGroup(
                rootPath,
                Relativize(trackedChunks, rootPath),
                Relativize(untrackedChunks, rootPath)));
        }

        return new GitDiffContext(comparisonBase) { FileGroups = groups };
    }

    /// <summary>Reports whether a scanned root is the repository itself or lives inside it.</summary>
    public bool IsInsideRepository(string rootPath)
    {
        var rootPrefix = VcsPathNormalizer.RootPrefix(rootPath);
        var repositoryPrefix = VcsPathNormalizer.RootPrefix(repositoryRoot);

        return repositoryPrefix.StartsWith(rootPrefix, Comparison) || rootPrefix.StartsWith(repositoryPrefix, Comparison);
    }

    /// <summary>Absolute path of a bundle root, which is also the key of its diff group.</summary>
    public static string RootPath(IFileInfo root) => string.IsNullOrEmpty(root.PhysicalPath) ? root.Name : root.PhysicalPath;

    private IReadOnlyList<DiffChunk> Relativize(IReadOnlyList<DiffChunk> chunks, string rootPath)
    {
        var relative = new List<DiffChunk>(chunks.Count);
        foreach (var chunk in chunks)
        {
            if (TryRelativize(chunk.RepositoryPath, rootPath, out var relativePath))
            {
                relative.Add(chunk with { Path = relativePath, Text = RelativizeChunkText(chunk, rootPath, relativePath) });
            }
        }

        return relative;
    }

    /// <summary>
    /// Rewrites the paths inside one chunk so the emitted patch matches the bundle root. The <c>diff --git</c>
    /// header and the <c>---</c>/<c>+++</c> lines are covered, which keeps the diff consistent with the file
    /// list. Text is only touched, never line separators.
    /// </summary>
    private string RelativizeChunkText(DiffChunk chunk, string rootPath, string relativePath)
    {
        // A root at the repository itself already sees git's own paths.
        if (RootPrefixOf(rootPath) == RootPrefixOf(repositoryRoot))
        {
            return chunk.Text;
        }

        var from = RootPrefixOf(chunk.RepositoryPath);
        var to = RootPrefixOf(relativePath);

        // The trailing-separator form covers "diff --git a/x/y b/x/y"; the bare form covers bare path usage.
        return chunk.Text
            .Replace(from, to, Comparison)
            .Replace(from.TrimEnd('/'), to.TrimEnd('/'), Comparison);
    }

    private bool TryRelativize(string repositoryRelativePath, string rootPath, out string relativePath)
    {
        relativePath = string.Empty;

        var rootPrefix = RootPrefixOf(rootPath);
        var anchored = VcsPathNormalizer.Anchor(repositoryRoot, repositoryRelativePath);
        if (!anchored.StartsWith(rootPrefix, Comparison))
        {
            return false;
        }

        // The prefix is used only for matching; slicing the original keeps the path casing intact.
        relativePath = anchored[rootPrefix.Length..];
        return true;
    }

    private string RootPrefixOf(string path) => VcsPathNormalizer.RootPrefix(path);

    private StringComparison Comparison => ignorePathCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
