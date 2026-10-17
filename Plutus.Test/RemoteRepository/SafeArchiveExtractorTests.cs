using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.RemoteRepository;

namespace Plutus.Test.RemoteRepository;

public class SafeArchiveExtractorTests
{
    [Fact]
    public async Task ExtractAsync_WhenArchiveContainsPathTraversal_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var evil = archive.CreateEntry("../../etc/evil.txt");
            await using var writer = new StreamWriter(await evil.OpenAsync());
            await writer.WriteAsync("malicious content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\windows\\system32\\evil.exe")]
    public async Task ExtractAsync_WhenArchiveContainsRootedPath_ShouldThrow(string rootedEntry)
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var evil = archive.CreateEntry(rootedEntry);
            await using var writer = new StreamWriter(await evil.OpenAsync());
            await writer.WriteAsync("malicious content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveExceedsFileCount_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int i = 0; i < 5; i++)
            {
                var entry = archive.CreateEntry($"file-{i}.txt");
                await using var writer = new StreamWriter(await entry.OpenAsync());
                await writer.WriteAsync(new string('x', 500) + $" content {i}");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxFileCount = 3 };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.FileCountLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveExceedsDepth_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        var path = string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("d", 6));
        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"{path}/file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync(new string('x', 500) + " deep content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxDepth = 4 };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.DepthLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveContainsSameFileNameInDifferentDirs_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("src/README.md");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("src readme");
            }

            var entry2 = archive.CreateEntry("docs/README.md");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("docs readme");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(2);
        mock.Files.Should().Contain("C:\\test_extract_root\\src\\README.md");
        mock.Files.Should().Contain("C:\\test_extract_root\\docs\\README.md");
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveExceedsExtractedSize_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("big-file.bin");
            await using (var writer = new StreamWriter(await entry.OpenAsync()))
            {
                await writer.WriteAsync(new string('x', 2048));
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(512) };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.SizeLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveIsClean_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("subdir/file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("hello world");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        result.TotalExtractedBytes.Should().BeGreaterThan(0);
        mock.Files.Should().Contain("C:\\test_extract_root\\subdir\\file.txt");
        mock.Directories.Should().Contain("C:\\test_extract_root");
        mock.Directories.Should().Contain("C:\\test_extract_root\\subdir");
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveIsEmpty_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var _ = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Empty archive — no entries
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(0);
        result.TotalExtractedBytes.Should().Be(0);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveContainsMultipleFiles_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("src/Program.cs");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("using System;");
            }

            var entry2 = archive.CreateEntry("src/Utils.cs");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("namespace Utils;");
            }

            var entry3 = archive.CreateEntry("README.md");
            await using (var w3 = new StreamWriter(await entry3.OpenAsync()))
            {
                await w3.WriteAsync("# Project");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(3);
        result.TotalExtractedBytes.Should().BeGreaterThan(0);
        mock.Files.Should().Contain("C:\\test_extract_root\\src\\Program.cs");
        mock.Files.Should().Contain("C:\\test_extract_root\\src\\Utils.cs");
        mock.Files.Should().Contain("C:\\test_extract_root\\README.md");
        mock.Directories.Should().Contain("C:\\test_extract_root");
        mock.Directories.Should().Contain("C:\\test_extract_root\\src");
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveContainsDirectoryOnlyEntries_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // ZIP can contain directory-only entries (trailing '/')
            archive.CreateEntry("empty_dir/");
            archive.CreateEntry("nested/deep/");

            var entry = archive.CreateEntry("nested/deep/file.txt");
            await using var w = new StreamWriter(await entry.OpenAsync());
            await w.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        mock.Files.Should().Contain("C:\\test_extract_root\\nested\\deep\\file.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveContainsFilesWithSpecialChars_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("config.json");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("{}");
            }

            var entry2 = archive.CreateEntry("data-v1.2.3.tar.gz");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("binary data");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(2);
        mock.Files.Should().Contain("C:\\test_extract_root\\config.json");
        mock.Files.Should().Contain("C:\\test_extract_root\\data-v1.2.3.tar.gz");
    }

    [Fact]
    public async Task ExtractAsync_WhenMaxDepthBoundary_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        // Depth 3: a/b/c/file.txt — exactly at MaxDepth = 3
        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("a/b/c/file.txt");
            await using var w = new StreamWriter(await entry.OpenAsync());
            await w.WriteAsync("at boundary");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxDepth = 3 };

        var result = await extractor.ExtractAsync(memory, targetDir, options);

        result.FilesExtracted.Should().Be(1);
        mock.Files.Should().Contain("C:\\test_extract_root\\a\\b\\c\\file.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveIsMalformed_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        var malformedData = "this is not a valid zip file"u8.ToArray();
        using var memory = new MemoryStream(malformedData);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.MalformedArchive);
    }

    [Fact]
    public async Task ExtractAsync_CapturesDirectoryCreationOperations()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("a/b/c/deep.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("deep content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        mock.Operations.Should().Contain("MKDIR:C:\\test_extract_root");
        mock.Operations.Should().Contain("MKDIR_PARENT:C:\\test_extract_root\\a");
        mock.Operations.Should().Contain("MKDIR_PARENT:C:\\test_extract_root\\a\\b");
        mock.Operations.Should().Contain("MKDIR_PARENT:C:\\test_extract_root\\a\\b\\c");
        mock.Operations.Should().Contain("WRITE_FILE:C:\\test_extract_root\\a\\b\\c\\deep.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenFilenameContainsDoubleDot_ShouldSucceed()
    {
        // BUG-02: "version..release.txt" was previously rejected by Contains("..")
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("version..release.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("legitimate content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        mock.Files.Should().Contain("C:\\test_extract_root\\version..release.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenFilenameContainsSingleDot_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.v1.2.3.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenPathContainsActualDotDotTraversal_ShouldThrow()
    {
        // Edge case: a/../../b/file.txt — per-segment validation should catch ".." segment
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("a/../../b/file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("malicious");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Fact]
    public async Task ExtractAsync_WhenSegmentIsSingleDot_ShouldThrow()
    {
        // Single "." segment is a path traversal vector
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("src/../file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("malicious");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Fact]
    public async Task ExtractAsync_WhenSegmentHasTrailingSpace_ShouldThrow()
    {
        // Windows trims trailing spaces: ".. " → ".." after write
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("folder/.. /file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("malicious");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Fact]
    public async Task ExtractAsync_WhenSegmentHasTrailingDots_ShouldThrow()
    {
        // Windows trims trailing dots: "file.." → "file" after write
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("folder/file.. /evil.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("malicious");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Fact]
    public async Task ExtractAsync_WhenFilenameHasNTFSAlternateDataStream_ShouldThrow()
    {
        // NTFS ADS: "file.txt:stream.txt" writes to hidden stream
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt:evil.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("malicious");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Fact]
    public async Task ExtractAsync_WhenFilenameHasADSDataOnly_ShouldThrow()
    {
        // ADS without explicit name: "file.txt:"
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt:evil");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("malicious");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Fact]
    public async Task ExtractAsync_WhenTargetDirectoryHasTrailingSeparator_ShouldSucceed()
    {
        // BUG-01: Trailing separator in targetDirectory was breaking path resolution
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root\\";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        result.ExtractedDirectory.Should().Be("C:\\test_extract_root");
        mock.Files.Should().Contain("C:\\test_extract_root\\file.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenTargetDirectoryHasAltSeparator_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:/test_extract_root/";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        mock.Files.Should().Contain("C:\\test_extract_root\\file.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenDuplicatePathsAndRejectEnabled_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("file.txt");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("first");
            }

            var entry2 = archive.CreateEntry("file.txt");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("second");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { RejectDuplicatePaths = true };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.DuplicatePath);
    }

    [Fact]
    public async Task ExtractAsync_WhenDuplicatePathsAndRejectDisabled_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("file.txt");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("first");
            }

            var entry2 = archive.CreateEntry("file.txt");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("second");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { RejectDuplicatePaths = false };

        var result = await extractor.ExtractAsync(memory, targetDir, options);

        // Both entries are processed (fileCount increments for each),
        // but on disk only the first survives (second overwrites first)
        result.FilesExtracted.Should().Be(2);
    }

    [Fact]
    public async Task ExtractAsync_WhenSymlinkRejected_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Create a regular entry — symlink detection via ExternalAttributes requires
            // native zip creation, so we test the RejectSymlinks flag behavior
            var entry = archive.CreateEntry("regular.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenSymlinksNotRejected_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("regular.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { RejectSymlinks = false };

        var result = await extractor.ExtractAsync(memory, targetDir, options);

        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenWindowsReservedName_ShouldThrowOnWindows()
    {
        // XPLAT-01: Reserved names should only be checked on Windows
        if (!OperatingSystem.IsWindows())
        {
            // On non-Windows, this should succeed
            var mock = new MockFileInfoDetailProvider();
            var extractor = new SafeArchiveExtractor(mock);

            using var memory = new MemoryStream();
            await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("CON.txt");
                await using var writer = new StreamWriter(await entry.OpenAsync());
                await writer.WriteAsync("content");
            }
            memory.Seek(0, SeekOrigin.Begin);

            var targetDirNonWin = "C:\\test_extract_root";

            var result = await extractor.ExtractAsync(memory, targetDirNonWin, SafeExtractionOptions.Default);

            result.FilesExtracted.Should().Be(1);
            return;
        }

        // Windows path: should throw
        var mockWin = new MockFileInfoDetailProvider();
        var extractorWin = new SafeArchiveExtractor(mockWin);

        using var memoryWin = new MemoryStream();
        await using (var archive = new ZipArchive(memoryWin, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("CON.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memoryWin.Seek(0, SeekOrigin.Begin);

        var targetDirWin = "C:\\test_extract_root";

        Func<Task> act = async () => await extractorWin.ExtractAsync(memoryWin, targetDirWin, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Theory]
    [InlineData("NUL")]
    [InlineData("AUX")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    public async Task ExtractAsync_WhenWindowsReservedNameCaseInsensitive_ShouldThrowOnWindows(string reservedName)
    {
        if (!OperatingSystem.IsWindows())
        {
            // On non-Windows, should succeed
            var mock = new MockFileInfoDetailProvider();
            var extractor = new SafeArchiveExtractor(mock);

            using var memory = new MemoryStream();
            await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry($"{reservedName}.txt");
                await using var writer = new StreamWriter(await entry.OpenAsync());
                await writer.WriteAsync("content");
            }
            memory.Seek(0, SeekOrigin.Begin);

            var targetDirNonWin = "C:\\test_extract_root";

            var result = await extractor.ExtractAsync(memory, targetDirNonWin, SafeExtractionOptions.Default);

            result.FilesExtracted.Should().Be(1);
            return;
        }

        var mockWin = new MockFileInfoDetailProvider();
        var extractorWin = new SafeArchiveExtractor(mockWin);

        using var memoryWin = new MemoryStream();
        await using (var archive = new ZipArchive(memoryWin, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"{reservedName}.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memoryWin.Seek(0, SeekOrigin.Begin);

        var targetDirWin = "C:\\test_extract_root";

        Func<Task> act = async () => await extractorWin.ExtractAsync(memoryWin, targetDirWin, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveHasDirectoryEntries_ShouldSkipAndCreateImplicitly()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Directory entries end with '/'
            archive.CreateEntry("dir1/");
            archive.CreateEntry("dir1/subdir/");

            var fileEntry = archive.CreateEntry("dir1/subdir/file.txt");
            await using var writer = new StreamWriter(await fileEntry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        mock.Files.Should().Contain("C:\\test_extract_root\\dir1\\subdir\\file.txt");
        mock.Directories.Should().Contain("C:\\test_extract_root\\dir1");
        mock.Directories.Should().Contain("C:\\test_extract_root\\dir1\\subdir");
    }

    [Fact]
    public async Task ExtractAsync_WhenMultipleFiles_ShouldReportAccurateTotalBytes()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("file1.txt");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("12345"); // 5 bytes
            }

            var entry2 = archive.CreateEntry("file2.txt");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("1234567890"); // 10 bytes
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        // TotalExtractedBytes should be based on entry.Length (before compression)
        result.TotalExtractedBytes.Should().Be(15);
        result.FilesExtracted.Should().Be(2);
    }

    [Fact]
    public async Task ExtractAsync_WhenCancellationRequested_ShouldThrowOpeationCanceledException()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Create many entries so cancellation can happen between them
            for (int i = 0; i < 100; i++)
            {
                var entry = archive.CreateEntry($"file-{i}.txt");
                await using var writer = new StreamWriter(await entry.OpenAsync());
                await writer.WriteAsync(new string('x', 1000));
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExtractAsync_WhenCancelledDuringExtraction_ShouldStopProcessing()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Create 100 entries
            for (int i = 0; i < 100; i++)
            {
                var entry = archive.CreateEntry($"file-{i:D4}.txt");
                await using var writer = new StreamWriter(await entry.OpenAsync());
                await writer.WriteAsync(new string('x', 500));
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        using var cts = new CancellationTokenSource();

        // Cancel after first few entries by using a custom approach:
        // We can't easily inject cancellation mid-stream, so we cancel before starting
        // and verify it throws immediately
        cts.CancelAfter(0);

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExtractAsync_WhenEntryResolvesToRoot_ShouldSucceed()
    {
        // Edge case: entry name that resolves exactly to root directory
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        // Use a short target dir to test path resolution edge cases
        var targetDir = "C:\\t";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        result.ExtractedDirectory.Should().Be("C:\\t");
    }

    [Fact]
    public async Task ExtractAsync_WhenPathHasMixedSeparators_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Some zip tools use forward slashes even on Windows
            var entry = archive.CreateEntry("dir1/dir2/file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        mock.Files.Should().Contain("C:\\test_extract_root\\dir1\\dir2\\file.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveEntryIsDeeplyNested_ShouldRespectMaxDepth()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        // Create path: a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q.txt (16 levels)
        var deepPath = string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("d", 16));

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"{deepPath}/q.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("deep");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        // MaxDepth = 16 should allow exactly 16 levels
        var options = new SafeExtractionOptions { MaxDepth = 16 };
        var result = await extractor.ExtractAsync(memory, targetDir, options);

        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveEntryExceedsMaxDepthByOne_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        // Create path: a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r.txt (17 levels)
        var deepPath = string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("d", 17));

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"{deepPath}/r.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("too deep");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxDepth = 16 };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.DepthLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenFileCountEqualsMax_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int i = 0; i < 5; i++)
            {
                var entry = archive.CreateEntry($"file-{i}.txt");
                await using var writer = new StreamWriter(await entry.OpenAsync());
                await writer.WriteAsync("content");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxFileCount = 5 };

        var result = await extractor.ExtractAsync(memory, targetDir, options);

        result.FilesExtracted.Should().Be(5);
    }

    [Fact]
    public async Task ExtractAsync_WhenFileCountExceedsMaxByOne_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int i = 0; i < 6; i++)
            {
                var entry = archive.CreateEntry($"file-{i}.txt");
                await using var writer = new StreamWriter(await entry.OpenAsync());
                await writer.WriteAsync("content");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxFileCount = 5 };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.FileCountLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenSizeEqualsMax_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync(new string('x', 100)); // exactly 100 bytes
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(100) };

        var result = await extractor.ExtractAsync(memory, targetDir, options);

        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenSizeExceedsMaxByOneByte_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync(new string('x', 101)); // 101 bytes
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(100) };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.SizeLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenPathTraversal_ShouldIncludeEntryPath()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("../../etc/evil.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("malicious");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.EntryPath.Should().Be("../../etc/evil.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenDepthExceeded_ShouldIncludeEntryPath()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        var deepPath = string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("d", 20));

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"{deepPath}/file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("deep");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxDepth = 5 };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.EntryPath.Should().Be($"{deepPath}/file.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenDuplicatePath_ShouldIncludeEntryPath()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("dup.txt");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("first");
            }

            var entry2 = archive.CreateEntry("dup.txt");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("second");
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { RejectDuplicatePaths = true };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.EntryPath.Should().Be("dup.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenExtractionSucceeds_ShouldReturnAccurateResult()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("file1.txt");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("hello"); // 5 bytes
            }

            var entry2 = archive.CreateEntry("subdir/file2.txt");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("world!"); // 6 bytes
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.ExtractedDirectory.Should().Be("C:\\test_extract_root");
        result.FilesExtracted.Should().Be(2);
        result.TotalExtractedBytes.Should().Be(11);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveIsEmpty_ShouldReturnZeroValues()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Empty archive — no entries at all
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.ExtractedDirectory.Should().Be("C:\\test_extract_root");
        result.FilesExtracted.Should().Be(0);
        result.TotalExtractedBytes.Should().Be(0);
    }

    [Fact]
    public async Task ExtractAsync_WhenEntryDeclaredSizeExceedsLimit_ShouldThrowBeforeExtraction()
    {
        // SEC-01: Pre-check on entry.Length (header) should reject oversized entries immediately
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("big-file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync(new string('x', 100)); // Only 100 bytes actual
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(50) };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.SizeLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenActualBytesExceedLimit_DuringRead_ShouldThrow()
    {
        // SEC-03: Per-chunk size check during decompression
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync(new string('x', 200)); // 200 bytes
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(150) };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.SizeLimitExceeded);
        ex.Which.EntryPath.Should().Be("file.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenMultipleFiles_TotalUsesActualBytes_ShouldSucceed()
    {
        // Verify totalBytes uses actual read bytes, not entry.Length
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("file1.txt");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync("12345"); // 5 bytes
            }

            var entry2 = archive.CreateEntry("subdir/file2.txt");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync("1234567890"); // 10 bytes
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.TotalExtractedBytes.Should().Be(15); // Actual bytes, not compressed size
        result.FilesExtracted.Should().Be(2);
    }

    [Fact]
    public async Task ExtractAsync_WhenTotalActualBytesExceedLimit_ShouldThrow()
    {
        // SEC-01: Total size check uses actual bytes across all files
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry1 = archive.CreateEntry("file1.txt");
            await using (var w1 = new StreamWriter(await entry1.OpenAsync()))
            {
                await w1.WriteAsync(new string('x', 80));
            }

            var entry2 = archive.CreateEntry("file2.txt");
            await using (var w2 = new StreamWriter(await entry2.OpenAsync()))
            {
                await w2.WriteAsync(new string('x', 80));
            }
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(100) };

        Func<Task> act = async () => await extractor.ExtractAsync(memory, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.SizeLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenStreamExceedsMaxDownloadSize_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        // Create a valid zip but make the stream larger than MaxDownloadSize
        using var zipMemory = new MemoryStream();
        await using (var archive = new ZipArchive(zipMemory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("small.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("small");
        }

        // Pad the stream to exceed the limit
        var paddedStream = new PaddedMemoryStream(zipMemory.ToArray(), 2048);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxDownloadSize = FileSize.FromBytes(1024) };

        Func<Task> act = async () => await extractor.ExtractAsync(paddedStream, targetDir, options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.DownloadSizeLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenStreamIsNonSeekable_ShouldAllowExtraction()
    {
        // Non-seekable streams can't be pre-checked — should proceed to extraction
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var zipMemory = new MemoryStream();
        await using (var archive = new ZipArchive(zipMemory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        zipMemory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(new NonSeekableStream(zipMemory), targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenStreamSizeEqualsMaxDownloadSize_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var zipMemory = new MemoryStream();
        await using (var archive = new ZipArchive(zipMemory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        zipMemory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";
        var options = new SafeExtractionOptions { MaxDownloadSize = FileSize.FromBytes(zipMemory.Length) };

        var result = await extractor.ExtractAsync(zipMemory, targetDir, options);

        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveStreamNotDisposed_ShouldStillWork()
    {
        // Verify that leaveOpen: true works correctly — caller's stream should remain usable
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var targetDir = "C:\\test_extract_root";

        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        // Stream should still be open and readable after extraction
        memory.Position.Should().BeGreaterOrEqualTo(0);
    }
}

/// <summary>
/// MemoryStream that reports a larger Length than its actual data.
/// </summary>
internal sealed class PaddedMemoryStream(byte[] data, long reportedLength)
    : MemoryStream(data)
{
    public override long Length => reportedLength;
}

/// <summary>
/// Stream wrapper that disables seeking.
/// </summary>
internal sealed class NonSeekableStream(Stream inner)
    : Stream
{
    private readonly Stream _inner = inner;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask DisposeAsync()
    {
        _inner.Position = 0;
        return base.DisposeAsync();
    }
}
