using System.Text;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diffing;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens;

namespace Plutus.Domain.BundleWriters;

public abstract class BaseBundleWriter(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IMarkdownLanguageProvider markdownLanguageProvider,
    ICodeMinifier codeMinifier,
    Encoding encoding,
    BundleLimits bundleLimits) : IBundleWriter
{
    private readonly BundleLimits _limits = bundleLimits;

    public abstract string FormatExtension { get; }
    public int MaxConcurrentFileReads { get; set; } = 8;
    public bool IncludeFileContents { get; set; } = true;

    protected readonly IFileInfoDetailProvider FileInfoDetailProvider = fileInfoDetailProvider;
    protected readonly IMarkdownLanguageProvider MarkdownLanguageProvider = markdownLanguageProvider;
    protected readonly ICodeMinifier CodeMinifier = codeMinifier;
    protected readonly Encoding Encoding = encoding;

    /// <summary>The limits this writer applies, so formats can report the same numbers the limits used.</summary>
    protected BundleLimits Limits => _limits;

    public async ValueTask<List<BundleEntry>> WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        GitDiffContext? gitDiff = null)
    {
        FileInfoDetailProvider.CreateDirectoryForFile(output);

        var entries = roots.SelectMany(e => e.ProjectNode.ExtractBundleEntries())
            .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.RelativePath, StringComparer.Ordinal)
            .ToList();

        using var semaphore = new SemaphoreSlim(MaxConcurrentFileReads);

        // ReSharper disable once AccessToDisposedClosure
        var processingTasks = entries.Select(e => ProcessEntryInternalAsync(semaphore, e));
        var processedFiles = (await Task.WhenAll(processingTasks).ConfigureAwait(false)).ToList();

        // The one shared limit step: size first, then the token budget, both from BundleLimits.
        var limitResult = _limits.Apply(processedFiles.Select(ToLimitItem).ToArray());
        var excluded = limitResult.Exclusions.ToDictionary(exclusion => exclusion.Key, exclusion => exclusion.Reason, StringComparer.Ordinal);
        var limitedFiles = processedFiles
            .Select(file => ApplyLimit(file, excluded, limitResult.IncludedKeys))
            .ToList();

        var tokenBudgetOmittedPaths = limitedFiles
            .Where(f => f.IsTokenBudgetOmitted)
            .Select(f => f.Entry.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var skippedPaths = limitedFiles
            .Where(f => f.IsSkipped && !f.IsTokenBudgetOmitted && !_limits.ExcludedBySize(f.Entry.File.Length))
            .Select(f => f.Entry.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            ApplyFileStatuses(root.ProjectNode, tokenBudgetOmittedPaths, skippedPaths);
        }

        await WriteContentAsync(roots, output, limitedFiles, _limits.CreateSummary(limitResult), gitDiff).ConfigureAwait(false);
        return entries;
    }

    protected abstract Task WriteContentAsync(List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        List<ProcessedFileContext> files,
        TokenBudgetSummary? tokenBudgetSummary,
        GitDiffContext? gitDiff);

    /// <summary>
    /// Describes one processed file to the limits service. A file the writer itself had to skip has no
    /// content to weigh, so it is ineligible by size and never takes part in the budget.
    /// </summary>
    private static BundleLimitItem ToLimitItem(ProcessedFileContext file) =>
        new(
            file.Entry.RelativePath,
            file.Entry.RelativePath,
            file.IsSkipped ? long.MaxValue : file.Entry.File.Length,
            file.EstimatedTokens ?? 0);

    /// <summary>
    /// Writes back the decision the limits service made. A file the writer itself had to skip keeps its own
    /// reason, which is more specific than a limit.
    /// </summary>
    private static ProcessedFileContext ApplyLimit(
        ProcessedFileContext file,
        Dictionary<string, string> excluded,
        IReadOnlyList<string> includedKeys)
    {
        if (file.IsSkipped)
        {
            return file;
        }

        if (includedKeys.Contains(file.Entry.RelativePath))
        {
            return file;
        }

        var reason = excluded.GetValueOrDefault(file.Entry.RelativePath, "excluded by bundle limits");
        var isSize = reason.Contains("max-file-size", StringComparison.Ordinal);

        return file with
        {
            Content = null,
            IsSkipped = true,
            IsTokenBudgetOmitted = !isSize,
            SkipReason = reason
        };
    }

    private void ApplyFileStatuses(
        ProjectNode node,
        HashSet<string> tokenBudgetOmittedPaths,
        HashSet<string> skippedPaths)
    {
        if (!node.IsDirectory)
        {
            if (node.Status != NodeStatus.Included)
            {
                return;
            }

            if (_limits.ExcludedBySize(node.FileInfo?.Length ?? 0))
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
            ApplyFileStatuses(child, tokenBudgetOmittedPaths, skippedPaths);
        }
    }

    private async Task<ProcessedFileContext> ProcessEntryInternalAsync(SemaphoreSlim semaphore, BundleEntry entry)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var language = MarkdownLanguageProvider.GetLanguage(entry.File.Name);

            if (_limits.ExcludedBySize(entry.File.Length))
            {
                return ProcessedFileContext.CreateSkipped(entry, language, _limits.SizeExclusionReason(entry.File.Length));
            }

            var (content, reason) = await ReadTextSafeAsync(entry.File).ConfigureAwait(false);
            if (content is null)
            {
                return ProcessedFileContext.CreateSkipped(entry, language, reason ?? "unknown error");
            }

            var minifiedContent = CodeMinifier.Minify(content, language);
            return ProcessedFileContext.CreateSuccess(entry, language, minifiedContent, _limits.EstimatesTokens ? TokenEstimator.Estimate(minifiedContent) : null);
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
