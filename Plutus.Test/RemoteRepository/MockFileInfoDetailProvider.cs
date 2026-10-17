using System.Text;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain;

namespace Plutus.Test.RemoteRepository;

/// <summary>
/// Captures all file-system operations without touching the real disk.
/// </summary>
public sealed class MockFileInfoDetailProvider : IFileInfoDetailProvider
{
    private readonly List<string> _operations = new();
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly HashSet<string> _files = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Operations => _operations;
    public IReadOnlyCollection<string> Directories => _directories;
    public IReadOnlyCollection<string> Files => _files;

    public IFileInfo CreateDirectory(string path)
    {
        _directories.Add(path);
        _operations.Add($"MKDIR:{path}");
        return new MockFileInfo(path, isDirectory: true);
    }

    public IFileInfo CreateFile(string path)
    {
        _files.Add(path);
        _operations.Add($"CREATE_FILE:{path}");
        return new MockFileInfo(path, isDirectory: false);
    }

    public void CreateDirectoryForFile(IFileInfo fileInfo)
    {
        var physicalPath = fileInfo.PhysicalPath;
        if (physicalPath is null)
        {
            return;
        }

        // Recursively create all parent directories
        var dir = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrEmpty(dir))
        {
            var parts = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var accumulated = string.Empty;
            foreach (var part in parts)
            {
                accumulated = string.IsNullOrEmpty(accumulated) ? part : Path.Combine(accumulated, part);
                if (!_directories.Contains(accumulated))
                {
                    _directories.Add(accumulated);
                    _operations.Add($"MKDIR_PARENT:{accumulated}");
                }
            }
        }
    }

    public StreamWriter CreateWriterForFile(IFileInfo fileInfo, Encoding? encoding = null)
    {
        var physicalPath = fileInfo.PhysicalPath ?? throw new InvalidOperationException("PhysicalPath is null");
        _operations.Add($"WRITE_FILE:{physicalPath}");
        return new StreamWriter(new MemoryStream(), encoding ?? Encoding.UTF8);
    }

    public IFileProvider CreateFileProvider(IFileInfo fileInfo) => throw new NotImplementedException();

    public bool IsReparsePoint(IFileInfo fileInfo) => false;

    public string GetCurrentDirectory() => Path.GetTempPath();
}

internal sealed class MockFileInfo(string path, bool isDirectory) : IFileInfo
{
    public bool Exists => true;
    public long Length => 0;
    public string PhysicalPath => path;
    public string Name => Path.GetFileName(path);
    public DateTimeOffset LastModified => DateTimeOffset.MinValue;
    public bool IsDirectory { get; } = isDirectory;

    public Stream CreateReadStream() => Stream.Null;
}
