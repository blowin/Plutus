namespace Plutus.Domain.Diffing;

/// <summary>
/// Membership test for a set of root-relative paths discovered through a diff. Directory queries answer
/// whether any changed file lives below the directory, which keeps the surrounding tree in the bundle
/// while excluding every unchanged file.
/// </summary>
public sealed class DiffPathSet
{
    private readonly HashSet<string> _filePaths;
    private readonly HashSet<string> _directoryPaths;

    private DiffPathSet(HashSet<string> filePaths, HashSet<string> directoryPaths)
    {
        _filePaths = filePaths;
        _directoryPaths = directoryPaths;
    }

    public int Count => _filePaths.Count;

    public static DiffPathSet Create(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        StringComparer comparison = StringComparer.OrdinalIgnoreCase;
        var filePaths = new HashSet<string>(comparison);
        var directoryPaths = new HashSet<string>(comparison);

        foreach (var path in paths)
        {
            var normalized = DiffPathNormalizer.Normalize(path);
            if (normalized.Length == 0 || !filePaths.Add(normalized))
            {
                continue;
            }

            for (var separator = normalized.IndexOf('/'); separator > 0; separator = normalized.IndexOf('/', separator + 1))
            {
                directoryPaths.Add(normalized[..separator]);
            }
        }

        return new DiffPathSet(filePaths, directoryPaths);
    }

    public bool ContainsFile(string relativePath) =>
        _filePaths.Contains(DiffPathNormalizer.Normalize(relativePath));

    public bool ContainsDirectory(string relativePath)
    {
        var normalized = DiffPathNormalizer.Normalize(relativePath);
        return normalized.Length == 0 || _directoryPaths.Contains(normalized);
    }
}
