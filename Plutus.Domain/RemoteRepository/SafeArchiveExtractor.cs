using System.IO.Compression;

namespace Plutus.Domain.RemoteRepository;

public sealed class SafeArchiveExtractor(IFileInfoDetailProvider fileSystem)
{
    private static readonly string[] WindowsReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    public async ValueTask<SafeExtractionResult> ExtractAsync(
        Stream archiveStream,
        string targetDirectory,
        SafeExtractionOptions options,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(targetDirectory);
        fileSystem.CreateDirectory(root);

        var extractedFiles = new HashSet<string>(StringComparer.Ordinal);
        long totalBytes = 0;
        int fileCount = 0;

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArchiveExtractionException(
                $"Failed to open archive: {ex.Message}",
                ArchiveExtractionErrorKind.MalformedArchive);
        }

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string entryName = entry.FullName;

            // Skip empty entries (zip root entries)
            if (string.IsNullOrEmpty(entryName))
            {
                continue;
            }

            // Skip directory-only entries (end with '/') — created implicitly by file extraction
            if (entryName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            // Reject absolute paths (Windows and Unix)
            if (Path.IsPathRooted(entryName))
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryName}' has an absolute path which is not allowed.",
                    ArchiveExtractionErrorKind.PathTraversal, entryName);
            }

            // Reject entries containing ".." path traversal sequences
            if (entryName.Contains(".."))
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryName}' contains path traversal sequence '..'.",
                    ArchiveExtractionErrorKind.PathTraversal, entryName);
            }

            // Resolve the full target path and verify it stays within root
            string entryFullPath = Path.GetFullPath(Path.Combine(root, entryName));

            string separator = Path.DirectorySeparatorChar.ToString();
            if (!entryFullPath.StartsWith(root + separator) && entryFullPath != root)
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryName}' would extract outside the target directory. " +
                    $"Resolved to '{entryFullPath}'.",
                    ArchiveExtractionErrorKind.PathTraversal, entryName);
            }

            // Symlink detection: on some platforms, symlinks are stored as directory entries
            // with non-zero length. Reject them for safety.
            if (options.RejectSymlinks && entry.Name.EndsWith("/", StringComparison.Ordinal) && entry.Length > 0)
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryName}' appears to be a symlink (non-zero length directory entry).",
                    ArchiveExtractionErrorKind.SymlinkEscape, entryName);
            }

            // Depth check: count directory separators in the relative path
            string relativePath = entryFullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
            int depth = relativePath.Count(c => c == Path.DirectorySeparatorChar);
            if (depth > options.MaxDepth)
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryName}' exceeds maximum depth of {options.MaxDepth} (actual: {depth}).",
                    ArchiveExtractionErrorKind.DepthLimitExceeded, entryName);
            }

            // File count check
            if (fileCount >= options.MaxFileCount)
            {
                throw new ArchiveExtractionException(
                    $"Archive contains more than {options.MaxFileCount} files ({fileCount} already extracted).",
                    ArchiveExtractionErrorKind.FileCountLimitExceeded);
            }

            // Duplicate path check (ordinal comparison catches exact duplicates)
            if (options.RejectDuplicatePaths && !extractedFiles.Add(entryName))
            {
                throw new ArchiveExtractionException(
                    $"Duplicate entry path: '{entryName}'.",
                    ArchiveExtractionErrorKind.DuplicatePath, entryName);
            }

            // Windows reserved device names (secondary defense)
            string baseName = Path.GetFileNameWithoutExtension(entryName);
            if (!string.IsNullOrEmpty(baseName) &&
                WindowsReservedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryName}' uses a reserved Windows device name '{baseName}'.",
                    ArchiveExtractionErrorKind.PathTraversal, entryName);
            }

            // Extract the file through IFileInfoDetailProvider
            try
            {
                var fileInfo = fileSystem.CreateFile(entryFullPath);
                fileSystem.CreateDirectoryForFile(fileInfo);

                await using var entryStream = await entry.OpenAsync(cancellationToken);
                await using var writer = fileSystem.CreateWriterForFile(fileInfo);
                await entryStream.CopyToAsync(writer.BaseStream, 81920, cancellationToken);
                await writer.FlushAsync(cancellationToken);

                totalBytes += entry.Length;
                fileCount++;
            }
            catch (ArchiveExtractionException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ArchiveExtractionException(
                    $"Failed to extract entry '{entryName}': {ex.Message}",
                    ArchiveExtractionErrorKind.MalformedArchive, entryName);
            }

            // Extracted size check after each file
            if (totalBytes > options.MaxExtractedSize.Bytes)
            {
                throw new ArchiveExtractionException(
                    $"Total extracted size ({totalBytes:N0} bytes) exceeds limit of {options.MaxExtractedSize}.",
                    ArchiveExtractionErrorKind.SizeLimitExceeded);
            }
        }

        return new SafeExtractionResult(root, fileCount, totalBytes);
    }
}

public sealed record SafeExtractionResult(
    string ExtractedDirectory,
    int FilesExtracted,
    long TotalExtractedBytes);

