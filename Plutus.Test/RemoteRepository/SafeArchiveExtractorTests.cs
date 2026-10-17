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
}
