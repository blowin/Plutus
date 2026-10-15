using System.Text;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diffing;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens;
using Plutus.Domain.Tokens.Applier;

namespace Plutus.Domain.BundleWriters;

public abstract class BaseBundleWriter(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IMarkdownLanguageProvider markdownLanguageProvider,
    ICodeMinifier codeMinifier,
    Encoding encoding,
    ITokenBudgetApplier tokenBudgetApplier) : IBundleWriter
{
    public abstract string FormatExtension { get; }
    public int MaxConcurrentFileReads { get; set; } = 8;
    public bool IncludeFileContents { get; set; } = true;

    protected readonly IFileInfoDetailProvider FileInfoDetailProvider = fileInfoDetailProvider;
    protected readonly IMarkdownLanguageProvider MarkdownLanguageProvider = markdownLanguageProvider;
    protected readonly ICodeMinifier CodeMinifier = codeMinifier;
    protected readonly Encoding Encoding = encoding;

    public async ValueTask<List<BundleEntry>> WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        FileSize maxFileSize,
        GitDiffContext? gitDiff = null,
        TokenBudgetSummary? diffBudgetSummary = null)
    {
        FileInfoDetailProvider.CreateDirectoryForFile(output);

        var entries = roots.SelectMany(e => e.ProjectNode.ExtractBundleEntries())
            .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.RelativePath, StringComparer.Ordinal)
            .ToList();

        using var semaphore = new SemaphoreSlim(MaxConcurrentFileReads);

        // ReSharper disable once AccessToDisposedClosure
        var processingTasks = entries.Select(e => ProcessEntryInternalAsync(semaphore, e, maxFileSize));
        var processedFiles = (await Task.WhenAll(processingTasks).ConfigureAwait(false)).ToList();

        var tokenBudgetResult = tokenBudgetApplier.Apply(processedFiles);
        var tokenBudgetOmittedPaths = tokenBudgetResult.Files
            .Where(f => f.IsTokenBudgetOmitted)
            .Select(f => f.Entry.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var skippedPaths = tokenBudgetResult.Files
            .Where(f => f.IsSkipped && !f.IsTokenBudgetOmitted && f.Entry.File.Length <= maxFileSize)
            .Select(f => f.Entry.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            ApplyFileStatuses(root.ProjectNode, maxFileSize, tokenBudgetOmittedPaths, skippedPaths);
        }

        // A diff scope replaces the content budget with its own, because the diffs are what the bundle holds.
        var budgetSummary = gitDiff is not null && diffBudgetSummary is not null
            ? diffBudgetSummary
            : tokenBudgetResult.Summary;

        await WriteContentAsync(roots, output, tokenBudgetResult.Files, maxFileSize, budgetSummary, gitDiff).ConfigureAwait(false);
        return entries;
    }

    protected abstract Task WriteContentAsync(List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        List<ProcessedFileContext> files,
        FileSize maxFileSize,
        TokenBudgetSummary? tokenBudgetSummary,
        GitDiffContext? gitDiff);

    private static void ApplyFileStatuses(
        ProjectNode node,
        FileSize maxFileSize,
        HashSet<string> tokenBudgetOmittedPaths,
        HashSet<string> skippedPaths)
    {
        if (!node.IsDirectory)
        {
            if (node.Status != NodeStatus.Included)
            {
                return;
            }

            if (node.FileInfo is { } file && file.Length > maxFileSize)
            {
                node.Status = NodeStatus.OversizedFile;
            }
            else if (tokenBudgetOmittedPaths.Contains(node.RelativePath))
            {
                node.Status = NodeStatus.TokenBudgetOmitted;
            }
            else if (skippedPaths.Contains(node.RelativePath))
            {
                node.Status = NodeStatus.SkippedFile;
            }

            return;
        }

        foreach (var child in node.Children)
        {
            ApplyFileStatuses(child, maxFileSize, tokenBudgetOmittedPaths, skippedPaths);
        }
    }

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
            return ProcessedFileContext.CreateSuccess(entry, language, minifiedContent, tokenBudgetApplier.IsEnabled ? TokenEstimator.Estimate(minifiedContent) : null);
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

}
