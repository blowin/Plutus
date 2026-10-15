using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Plutus.Test.Fakes;

/// <summary>
/// An <see cref="IFileProvider"/> over an in-memory tree, so a test of code that reads files describes the tree it
/// wants instead of creating one on disk: no temporary directories, no cleanup, no interference between tests that
/// run in parallel.
/// </summary>
public sealed class InMemoryFileProvider : IFileProvider
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { string.Empty };

    /// <summary>Adds a file, creating the directories that lead to it.</summary>
    public InMemoryFileProvider AddFile(string path, string content)
    {
        var normalized = Normalize(path);
        _files[normalized] = content;

        var directory = Parent(normalized);
        while (directory is not null)
        {
            _directories.Add(directory);
            directory = Parent(directory);
        }

        return this;
    }

    /// <summary>Adds an empty directory, which is what a marker such as <c>.git</c> needs.</summary>
    public InMemoryFileProvider AddDirectory(string path)
    {
        var normalized = Normalize(path);
        _directories.Add(normalized);

        var directory = Parent(normalized);
        while (directory is not null)
        {
            _directories.Add(directory);
            directory = Parent(directory);
        }

        return this;
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var normalized = Normalize(subpath);
        if (_files.TryGetValue(normalized, out var content))
        {
            return new InMemoryFileInfo(normalized, content, isDirectory: false);
        }

        return _directories.Contains(normalized)
            ? new InMemoryFileInfo(normalized, content: null, isDirectory: true)
            : new NotFoundFileInfo(normalized);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var normalized = Normalize(subpath);
        if (!_directories.Contains(normalized))
        {
            return NotFoundDirectoryContents.Singleton;
        }

        var prefix = normalized.Length == 0 ? string.Empty : $"{normalized}/";
        var names = _files.Keys
            .Concat(_directories)
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal) && path.Length > prefix.Length)
            .Select(path => path[prefix.Length..])
            .Where(rest => !rest.Contains('/'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => GetFileInfo($"{prefix}{name}"))
            .ToList();

        return new InMemoryDirectoryContents(names);
    }

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');

    private static string? Parent(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? null : path[..separator];
    }

    private sealed class InMemoryFileInfo(string name, string? content, bool isDirectory) : IFileInfo
    {
        public bool Exists => true;

        public long Length => content?.Length ?? 0;

        public string? PhysicalPath => null;

        public string Name => name[(name.LastIndexOf('/') + 1)..];

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public bool IsDirectory { get; } = isDirectory;

        public Stream CreateReadStream() => content is null
            ? throw new FileNotFoundException($"'{name}' is a directory")
            : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
    }

    private sealed class InMemoryDirectoryContents(IReadOnlyList<IFileInfo> entries) : IDirectoryContents
    {
        public bool Exists => true;

        public IEnumerator<IFileInfo> GetEnumerator() => entries.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
