using System.Buffers;
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
        string root = Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // MaxDownloadSize: reject oversized archives before processing
        if (archiveStream.CanSeek && archiveStream.Length > options.MaxDownloadSize.Bytes)
        {
            throw new ArchiveExtractionException(
                $"Archive size ({archiveStream.Length:N0} bytes) exceeds download limit of {options.MaxDownloadSize}.",
                ArchiveExtractionErrorKind.DownloadSizeLimitExceeded);
        }

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

        await using var archiveDisposable = archive;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string entryFullName = entry.FullName;

            // Skip empty entries (zip root entries)
            if (string.IsNullOrEmpty(entryFullName))
            {
                continue;
            }

            // Skip directory-only entries (end with '/') — created implicitly by file extraction
            if (entryFullName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            // Reject absolute paths (Windows and Unix)
            if (Path.IsPathRooted(entryFullName))
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryFullName}' has an absolute path which is not allowed.",
                    ArchiveExtractionErrorKind.PathTraversal, entryFullName);
            }

            // Resolve the full target path and verify it stays within root
            string entryFullPath;
            try
            {
                entryFullPath = Path.GetFullPath(Path.Combine(root, entryFullName));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ArchiveExtractionException(
                    $"Failed to resolve path for entry '{entryFullName}': {ex.Message}",
                    ArchiveExtractionErrorKind.PathTraversal, entryFullName);
            }

            if (ContainsPathTraversal(entryFullName))
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryFullName}' contains path traversal sequence.",
                    ArchiveExtractionErrorKind.PathTraversal, entryFullName);
            }

            string separator = Path.DirectorySeparatorChar.ToString();
            if (!entryFullPath.StartsWith(root + separator) && entryFullPath != root)
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryFullName}' would extract outside the target directory. " +
                    $"Resolved to '{entryFullPath}'.",
                    ArchiveExtractionErrorKind.PathTraversal, entryFullName);
            }

            if (options.RejectSymlinks && IsSymlink(entry))
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryFullName}' appears to be a symlink.",
                    ArchiveExtractionErrorKind.SymlinkEscape, entryFullName);
            }

            // Depth check: ZIP can use '/' or '\', count both for cross-platform reliability
            int depth = entryFullName.Count(c => c == '/' || c == Path.DirectorySeparatorChar);
            if (depth > options.MaxDepth)
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryFullName}' exceeds maximum depth of {options.MaxDepth} (actual: {depth}).",
                    ArchiveExtractionErrorKind.DepthLimitExceeded, entryFullName);
            }

            // File count check
            if (fileCount >= options.MaxFileCount)
            {
                throw new ArchiveExtractionException(
                    $"Archive contains more than {options.MaxFileCount} files ({fileCount} already extracted).",
                    ArchiveExtractionErrorKind.FileCountLimitExceeded);
            }

            // Duplicate path check (ordinal comparison catches exact duplicates)
            if (options.RejectDuplicatePaths && !extractedFiles.Add(entryFullName))
            {
                throw new ArchiveExtractionException(
                    $"Duplicate entry path: '{entryFullName}'.",
                    ArchiveExtractionErrorKind.DuplicatePath, entryFullName);
            }

            if (OperatingSystem.IsWindows())
            {
                string baseName = Path.GetFileNameWithoutExtension(entryFullName);
                if (!string.IsNullOrEmpty(baseName) &&
                    WindowsReservedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArchiveExtractionException(
                        $"Entry '{entryFullName}' uses a reserved Windows device name '{baseName}'.",
                        ArchiveExtractionErrorKind.PathTraversal, entryFullName);
                }
            }

            // Real size enforcement happens during extraction via actual read bytes.
            if (entry.Length > options.MaxExtractedSize.Bytes)
            {
                throw new ArchiveExtractionException(
                    $"Entry '{entryFullName}' declared size ({entry.Length:N0} bytes) exceeds limit of {options.MaxExtractedSize}.",
                    ArchiveExtractionErrorKind.SizeLimitExceeded, entryFullName);
            }

            // Extract the file through IFileInfoDetailProvider
            try
            {
                var fileInfo = fileSystem.CreateFile(entryFullPath);
                fileSystem.CreateDirectoryForFile(fileInfo);

                await using var entryStream = await entry.OpenAsync(cancellationToken);
                await using var writer = fileSystem.CreateWriterForFile(fileInfo);

                // Uses ArrayPool.Shared like Stream.CopyToAsync internally, but with per-chunk size enforcement.
                long actualBytesRead = 0;
                byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
                try
                {
                    int bytesRead;
                    while ((bytesRead = await entryStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await writer.BaseStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        actualBytesRead += bytesRead;

                        // Per-chunk size check to prevent Zip Bomb expansion attacks
                        if (actualBytesRead > options.MaxExtractedSize.Bytes)
                        {
                            throw new ArchiveExtractionException(
                                $"Entry '{entryFullName}' expanded to {actualBytesRead:N0} bytes, " +
                                $"exceeding limit of {options.MaxExtractedSize}.",
                                ArchiveExtractionErrorKind.SizeLimitExceeded, entryFullName);
                        }
                    }
                    await writer.FlushAsync(cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                totalBytes += actualBytesRead;
                fileCount++;
            }
            catch (ArchiveExtractionException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ArchiveExtractionException(
                    $"Failed to extract entry '{entryFullName}': {ex.Message}",
                    ArchiveExtractionErrorKind.MalformedArchive, entryFullName,
                    ex);
            }

            // Extracted size check after each file (uses actual bytes, not header)
            if (totalBytes > options.MaxExtractedSize.Bytes)
            {
                throw new ArchiveExtractionException(
                    $"Total extracted size ({totalBytes:N0} bytes) exceeds limit of {options.MaxExtractedSize}.",
                    ArchiveExtractionErrorKind.SizeLimitExceeded);
            }
        }

        return new SafeExtractionResult(root, fileCount, totalBytes);
    }

    private static bool ContainsPathTraversal(string path)
    {
        // ZIP внутри архива ВСЕГДА использует '/' как разделитель
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            if (segment == ".." || segment == ".")
            {
                return true;
            }

            // Защита от обхода через пробелы/точки на конце (Windows обрезает) и ADS (NTFS)
            if (segment.EndsWith(' ') || segment.EndsWith('.') || segment.Contains(':'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSymlink(ZipArchiveEntry entry)
    {
#if NET8_0_OR_GREATER
        // S_ISLNK = 0xA000 (Unix symlink mask)
        int unixAttributes = entry.ExternalAttributes >> 16;
        return (unixAttributes & 0xF000) == 0xA000;
#else
    return false;
#endif
    }
}

public sealed record SafeExtractionResult(
    string ExtractedDirectory,
    int FilesExtracted,
    long TotalExtractedBytes);

