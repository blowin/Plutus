using System.IO.Compression;
using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.RemoteRepository;

namespace Plutus.Test.RemoteRepository;

public class SafeArchiveExtractorTests
{
    [Theory]
    [InlineData("../../etc/evil.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\windows\\system32\\evil.exe")]
    [InlineData("a/../../b/file.txt")]
    [InlineData("src/../file.txt")]
    [InlineData("folder/.. /file.txt")]       // Trailing space bypass
    [InlineData("folder/file.. /evil.txt")]   // Trailing dot bypass
    [InlineData("file.txt:evil.txt")]         // NTFS ADS
    [InlineData("file.txt:")]                 // NTFS ADS (no name)
    public async Task ExtractAsync_WhenPathIsMalicious_ShouldThrowPathTraversal(string maliciousEntry)
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(maliciousEntry);
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("malicious");
        }
        memory.Seek(0, SeekOrigin.Begin);

        Func<Task> act = async () => await extractor.ExtractAsync(memory, "C:\\test_extract_root", SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
        ex.Which.EntryPath.Should().Be(maliciousEntry);
    }

    [Fact]
    public async Task ExtractAsync_WhenFilenameContainsLegitimateDots_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("version..release.txt").Open().Dispose();
            archive.CreateEntry("file.v1.2.3.txt").Open().Dispose();
        }
        memory.Seek(0, SeekOrigin.Begin);

        var result = await extractor.ExtractAsync(memory, "C:\\test_extract_root", SafeExtractionOptions.Default);
        result.FilesExtracted.Should().Be(2);
    }

    [Fact]
    public async Task ExtractAsync_WhenExtractedSizeLimitExceeded_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(100) };

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Header says 50, actual is 101 (triggers chunked read limit)
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync(new string('x', 101));
        }
        memory.Seek(0, SeekOrigin.Begin);

        var act = async () => await extractor.ExtractAsync(memory, "C:\\test", options);
        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.SizeLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenWindowsReservedName_ShouldThrowOnWindowsOnly()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("CON.txt").Open().Dispose();
        }
        memory.Seek(0, SeekOrigin.Begin);

        if (OperatingSystem.IsWindows())
        {
            var act = async () => await extractor.ExtractAsync(memory, "C:\\test", SafeExtractionOptions.Default);
            var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
            ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.PathTraversal);
        }
        else
        {
            var result = await extractor.ExtractAsync(memory, "C:\\test", SafeExtractionOptions.Default);
            result.FilesExtracted.Should().Be(1);
        }
    }

    [Fact]
    public async Task ExtractAsync_WhenDuplicatePathsCaseInsensitiveOnWindows_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { RejectDuplicatePaths = true };

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("file.txt").Open().Dispose();
            archive.CreateEntry("FILE.TXT").Open().Dispose();
        }
        memory.Seek(0, SeekOrigin.Begin);

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            var act = async () => await extractor.ExtractAsync(memory, "C:\\test", options);
            var res = await act.Should().ThrowAsync<ArchiveExtractionException>();
            res.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.DuplicatePath);
        }
        else
        {
            var result = await extractor.ExtractAsync(memory, "C:\\test", options);
            result.FilesExtracted.Should().Be(2); // Linux is case-sensitive
        }
    }

    [Theory]
    [InlineData("C:\\test_extract_root\\")]
    [InlineData("C:/test_extract_root/")]
    [InlineData("C:\\t")] // Short path edge case
    public async Task ExtractAsync_WhenTargetDirectoryHasEdgeCases_ShouldSucceed(string targetDir)
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("file.txt").Open().Dispose();
        }
        memory.Seek(0, SeekOrigin.Begin);

        var expectedRoot = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var result = await extractor.ExtractAsync(memory, targetDir, SafeExtractionOptions.Default);

        result.FilesExtracted.Should().Be(1);
        result.ExtractedDirectory.Should().Be(expectedRoot);
    }

    [Fact]
    public async Task ExtractAsync_WhenStreamExceedsMaxDownloadSize_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { MaxDownloadSize = FileSize.FromBytes(10) };

        using var zipMemory = new MemoryStream();
        await using (var archive = new ZipArchive(zipMemory, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("small.txt").Open().Dispose();
        }

        var paddedStream = new PaddedMemoryStream(zipMemory.ToArray(), 100); // Fake large size

        var act = async () => await extractor.ExtractAsync(paddedStream, "C:\\test", options);
        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.DownloadSizeLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenStreamIsNonSeekable_ShouldSkipDownloadCheckAndSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);

        using var zipMemory = new MemoryStream();
        await using (var archive = new ZipArchive(zipMemory, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("file.txt").Open().Dispose();
        }
        zipMemory.Seek(0, SeekOrigin.Begin);

        var result = await extractor.ExtractAsync(new NonSeekableStream(zipMemory), "C:\\test", SafeExtractionOptions.Default);
        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenCancellationRequested_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("file.txt").Open().Dispose();
        }
        memory.Seek(0, SeekOrigin.Begin);

        var act = async () => await extractor.ExtractAsync(memory, "C:\\test", SafeExtractionOptions.Default, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExtractAsync_WhenArchiveIsMalformed_ShouldThrowWithInnerException()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        using var memory = new MemoryStream("not a zip"u8.ToArray());

        var act = async () => await extractor.ExtractAsync(memory, "C:\\test", SafeExtractionOptions.Default);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.MalformedArchive);
        ex.Which.InnerException.Should().NotBeNull(); // Verifies inner exception is preserved
    }

    [Fact]
    public async Task ExtractAsync_WhenFileCountEqualsMax_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { MaxFileCount = 5 };

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

        var result = await extractor.ExtractAsync(memory, "C:\\test", options);

        result.FilesExtracted.Should().Be(5);
    }

    [Fact]
    public async Task ExtractAsync_WhenFileCountExceedsMax_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { MaxFileCount = 5 };

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

        var act = async () => await extractor.ExtractAsync(memory, "C:\\test", options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.FileCountLimitExceeded);
    }

    [Fact]
    public async Task ExtractAsync_WhenDepthEqualsMax_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { MaxDepth = 3 };

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 3 разделителя '/', значит глубина = 3
            var entry = archive.CreateEntry("a/b/c/file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var result = await extractor.ExtractAsync(memory, "C:\\test", options);

        result.FilesExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_WhenDepthExceedsMax_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { MaxDepth = 3 };

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 4 разделителя '/', значит глубина = 4 (превышает лимит 3)
            var entry = archive.CreateEntry("a/b/c/d/file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync("content");
        }
        memory.Seek(0, SeekOrigin.Begin);

        var act = async () => await extractor.ExtractAsync(memory, "C:\\test", options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.DepthLimitExceeded);
        ex.Which.EntryPath.Should().Be("a/b/c/d/file.txt");
    }

    [Fact]
    public async Task ExtractAsync_WhenExtractedSizeEqualsMax_ShouldSucceed()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(100) };

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync(new string('x', 100)); // ровно 100 байт
        }
        memory.Seek(0, SeekOrigin.Begin);

        var result = await extractor.ExtractAsync(memory, "C:\\test", options);

        result.FilesExtracted.Should().Be(1);
        result.TotalExtractedBytes.Should().Be(100);
    }

    [Fact]
    public async Task ExtractAsync_WhenExtractedSizeExceedsMax_ShouldThrow()
    {
        var mock = new MockFileInfoDetailProvider();
        var extractor = new SafeArchiveExtractor(mock);
        var options = new SafeExtractionOptions { MaxExtractedSize = FileSize.FromBytes(100) };

        using var memory = new MemoryStream();
        await using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("file.txt");
            await using var writer = new StreamWriter(await entry.OpenAsync());
            await writer.WriteAsync(new string('x', 101)); // 101 байт (превысит лимит при чтении чанка)
        }
        memory.Seek(0, SeekOrigin.Begin);

        var act = async () => await extractor.ExtractAsync(memory, "C:\\test", options);

        var ex = await act.Should().ThrowAsync<ArchiveExtractionException>();
        ex.Which.ErrorKind.Should().Be(ArchiveExtractionErrorKind.SizeLimitExceeded);
        ex.Which.EntryPath.Should().Be("file.txt");
    }

    internal sealed class PaddedMemoryStream(byte[] data, long reportedLength) : MemoryStream(data)
    {
        public override long Length => reportedLength;
    }

    internal sealed class NonSeekableStream(Stream inner) : Stream
    {
        private readonly Stream _inner = inner;
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override ValueTask DisposeAsync() { _inner.Position = 0; return base.DisposeAsync(); }
    }
}
