using System.Text;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens;

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
    protected TokenBudgetSummary? TokenBudgetSummary { get; private set; }

    public async ValueTask<List<BundleEntry>> WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        RunAppOptions options)
    {
        FileInfoDetailProvider.CreateDirectoryForFile(output);

        var entries = roots.SelectMany(e => e.ProjectNode.ExtractBundleEntries())
            .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.RelativePath, StringComparer.Ordinal)
            .ToList();

        using var semaphore = new SemaphoreSlim(MaxConcurrentFileReads);
        // ReSharper disable once AccessToDisposedClosure
        var processingTasks = entries.Select(e => ProcessEntryInternalAsync(semaphore, e, options));
        IReadOnlyCollection<ProcessedFileContext> processedFiles = await Task.WhenAll(processingTasks).ConfigureAwait(false);

        if (options?.MaxTokens is int maxTokens)
        {
            var tokenBudget = new TokenBudgetOptions(maxTokens, options.TokenOverflowPolicy, options.TokenSelectionStrategy);
            processedFiles = ApplyTokenBudget(processedFiles, tokenBudget);
        }

        await WriteContentAsync(roots, output, processedFiles, options.MaxFileSize).ConfigureAwait(false);

        return entries;
    }

    protected abstract Task WriteContentAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        IReadOnlyCollection<ProcessedFileContext> files,
        FileSize maxFileSize);

    private async Task<ProcessedFileContext> ProcessEntryInternalAsync(SemaphoreSlim semaphore, BundleEntry entry, RunAppOptions options)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var language = MarkdownLanguageProvider.GetLanguage(entry.File.Name);

            if (entry.File.Length > options.MaxFileSize)
            {
                return ProcessedFileContext.CreateSkipped(entry, language, $"file is larger than max-file-size ({entry.File.Length.HumanSize()} > {options.MaxFileSize})");
            }

            var (content, reason) = await ReadTextSafeAsync(entry.File).ConfigureAwait(false);
            if (content is null)
            {
                return ProcessedFileContext.CreateSkipped(entry, language, reason ?? "unknown error");
            }

            var minifiedContent = CodeMinifier.Minify(content, language);
            return ProcessedFileContext.CreateSuccess(entry, language, minifiedContent, options.MaxTokens.HasValue ? TokenEstimator.Estimate(minifiedContent) : null);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private IReadOnlyCollection<ProcessedFileContext> ApplyTokenBudget(IReadOnlyCollection<ProcessedFileContext> files, TokenBudgetOptions budget)
    {
        if (budget.MaxTokens < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), "Max tokens must be at least 1.");
        }

        var policy = budget.OverflowPolicy.ToLowerInvariant();
        if (policy is not ("fail" or "warn" or "select"))
        {
            throw new ArgumentException($"Unknown token overflow policy '{budget.OverflowPolicy}'. Use fail, warn, or select.", nameof(budget));
        }

        var strategy = budget.SelectionStrategy.ToLowerInvariant();
        if (strategy is not ("path-order" or "largest-first"))
        {
            throw new ArgumentException($"Unknown token selection strategy '{budget.SelectionStrategy}'. Use path-order or largest-first.", nameof(budget));
        }

        var candidateTokens = files.Where(f => !f.IsSkipped).Sum(f => f.EstimatedTokens ?? 0);
        var selected = files.ToList();
        var selectedTokens = candidateTokens;

        if (candidateTokens > budget.MaxTokens && policy == "fail")
        {
            PrintTokenReport(files, budget, candidateTokens, 0);
            throw new TokenBudgetExceededException($"Estimated source usage ({candidateTokens:N0} tokens) exceeds --max-tokens {budget.MaxTokens:N0}; no bundle was written. Choose --token-overflow-policy warn or select to continue.");
        }

        if (candidateTokens > budget.MaxTokens && policy == "warn")
        {
            Console.Error.WriteLine($"Warning: estimated source usage ({candidateTokens:N0} tokens) exceeds --max-tokens {budget.MaxTokens:N0}; all eligible files will be included.");
        }
        else if (candidateTokens > budget.MaxTokens && policy == "select")
        {
            var candidates = files.Where(f => !f.IsSkipped);
            candidates = strategy == "largest-first"
                ? candidates.OrderByDescending(f => f.EstimatedTokens).ThenBy(f => f.Entry.RelativePath, StringComparer.Ordinal)
                : candidates.OrderBy(f => f.Entry.RelativePath, StringComparer.Ordinal);

            var selectedEntries = new HashSet<BundleEntry>();
            selectedTokens = 0;
            foreach (var file in candidates)
            {
                var estimate = file.EstimatedTokens ?? 0;
                if (estimate <= budget.MaxTokens - selectedTokens)
                {
                    selectedEntries.Add(file.Entry);
                    selectedTokens = estimate;
                }
            }

            selected = files.Select(file => !file.IsSkipped && !selectedEntries.Contains(file.Entry)
                ? file with { Content = null, IsSkipped = true, SkipReason = $"omitted by token budget ({budget.MaxTokens} tokens; strategy: {strategy})" }
                : file).ToList();
        }

        TokenBudgetSummary = new TokenBudgetSummary(budget.MaxTokens, policy, strategy, candidateTokens, selectedTokens, TokenEstimator.Method);
        PrintTokenReport(files, budget, candidateTokens, selectedTokens);
        return selected;
    }

    private static void PrintTokenReport(IReadOnlyCollection<ProcessedFileContext> files, TokenBudgetOptions budget, int candidateTokens, int selectedTokens)
    {
        Console.WriteLine($"Token estimate method: {TokenEstimator.Method}");
        foreach (var file in files.OrderBy(f => f.Entry.RelativePath, StringComparer.Ordinal))
        {
            var estimate = file.EstimatedTokens.HasValue ? $"{file.EstimatedTokens.Value:N0}" : "n/a (skipped before reading)";
            Console.WriteLine($"Tokens: {estimate,-28} {file.Entry.RelativePath}");
        }
        Console.WriteLine($"Estimated source tokens: {candidateTokens:N0}; selected: {selectedTokens:N0}; budget: {budget.MaxTokens:N0}; policy: {budget.OverflowPolicy}; strategy: {budget.SelectionStrategy}.");
        Console.WriteLine("Note: estimates cover readable file contents after optional minification, not bundle headings, tree/list metadata, or tokenizer-specific overhead.");
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
        string? SkipReason,
        int? EstimatedTokens = null)
    {
        public static ProcessedFileContext CreateSuccess(BundleEntry entry, string lang, string content, int? estimatedTokens) =>
            new(entry, lang, content, false, null, estimatedTokens);

        public static ProcessedFileContext CreateSkipped(BundleEntry entry, string lang, string reason) =>
            new(entry, lang, null, true, reason, null);
    }
}
