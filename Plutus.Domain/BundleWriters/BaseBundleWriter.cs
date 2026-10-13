using System.Text;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Minifier;

namespace Plutus.Domain.BundleWriters;

public abstract class BaseBundleWriter(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IMarkdownLanguageProvider markdownLanguageProvider,
    ICodeMinifier codeMinifier,
    Encoding encoding) : IBundleWriter
{
    public abstract string FormatExtension { get; }
    public int MaxConcurrentFileReads { get; set; } = 8;

    protected readonly IFileInfoDetailProvider FileInfoDetailProvider = fileInfoDetailProvider;
    protected readonly IMarkdownLanguageProvider MarkdownLanguageProvider = markdownLanguageProvider;
    protected readonly ICodeMinifier CodeMinifier = codeMinifier;
    protected readonly Encoding Encoding = encoding;

    public async ValueTask<List<BundleEntry>> WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        FileSize maxFileSize)
    {
        FileInfoDetailProvider.CreateDirectoryForFile(output);

        var entries = roots.SelectMany(e => e.ProjectNode.ExtractBundleEntries()).ToList();
        entries.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

        using var semaphore = new SemaphoreSlim(MaxConcurrentFileReads);
        // ReSharper disable once AccessToDisposedClosure
        var processingTasks = entries.Select(e => ProcessEntryInternalAsync(semaphore, e, maxFileSize));
        var processedFiles = await Task.WhenAll(processingTasks).ConfigureAwait(false);

        await WriteContentAsync(roots, output, processedFiles.ToList(), maxFileSize).ConfigureAwait(false);

        return entries;
    }

    protected abstract Task WriteContentAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        List<ProcessedFileContext> files,
        FileSize maxFileSize);

    private async Task<ProcessedFileContext> ProcessEntryInternalAsync(SemaphoreSlim semaphore, BundleEntry entry, FileSize maxFileSize)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var language = MarkdownLanguageProvider.GetLanguage(entry.File.Name);

            if (entry.File.Length > maxFileSize)
            {
                return ProcessedFileContext.CreateSkipped(entry, language, $"file is larger than max-file-size ({entry.File.Length.HumanSize()} > {maxFileSize})");
            }

            var (content, reason) = await ReadTextSafeAsync(entry.File).ConfigureAwait(false);
            if (content is null)
            {
                return ProcessedFileContext.CreateSkipped(entry, language, reason ?? "unknown error");
            }

            var minifiedContent = CodeMinifier.Minify(content, language);
            return ProcessedFileContext.CreateSuccess(entry, language, minifiedContent);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<(string? Content, string? Reason)> ReadTextSafeAsync(IFileInfo file)
    {
        try
        {
            await using (var fs = file.CreateReadStream())
            {
                var buffer = new byte[8192];
                var bytesRead = await fs.ReadAsync(buffer).ConfigureAwait(false);
                for (var i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0)
                    {
                        return (null, "binary file");
                    }
                }
            }

            byte[] bytes;
            await using (var ms = new MemoryStream())
            {
                await using (var fs = file.CreateReadStream())
                {
                    await fs.CopyToAsync(ms).ConfigureAwait(false);
                }
                bytes = ms.ToArray();
            }

            if (bytes.Length == 0)
            {
                return (string.Empty, null);
            }

            return (Encoding.GetString(bytes), null);
        }
        catch (Exception ex)
        {
            return (null, $"unreadable: {ex.Message}");
        }
    }

    protected record ProcessedFileContext(
        BundleEntry Entry, 
        string Language, 
        string? Content, 
        bool IsSkipped, 
        string? SkipReason)
    {
        public static ProcessedFileContext CreateSuccess(BundleEntry entry, string lang, string content) =>
            new(entry, lang, content, false, null);

        public static ProcessedFileContext CreateSkipped(BundleEntry entry, string lang, string reason) =>
            new(entry, lang, null, true, reason);
    }
}
