using Plutus.Domain.BundleWriters;

namespace Plutus.Domain.Tokens.Applier;

/// <summary>Applies deterministic whole-file token-budget policies.</summary>
public sealed class TokenBudgetApplier(TokenBudgetOptions budget) : ITokenBudgetApplier
{
    public bool IsEnabled => true;
    public TokenBudgetResult Apply(IReadOnlyList<ProcessedFileContext> sourceFiles)
    {
        if (budget.MaxTokens < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), "Max tokens must be at least 1.");
        }

        var files = sourceFiles.ToList();
        var strategy = budget.SelectionStrategy;
        if (!Enum.IsDefined(strategy))
        {
            throw new ArgumentOutOfRangeException(nameof(budget), $"Unknown token selection strategy '{strategy}'.");
        }

        var candidateTokens = files.Where(f => !f.IsSkipped).Sum(f => f.EstimatedTokens ?? 0);
        var selected = files;
        var selectedTokens = candidateTokens;

        if (candidateTokens > budget.MaxTokens && budget.OverflowPolicy == TokenOverflowPolicy.Fail)
        {
            PrintTokenReport(files, candidateTokens, 0);
            throw new TokenBudgetExceededException($"Estimated source usage ({candidateTokens:N0} tokens) exceeds --max-tokens {budget.MaxTokens:N0}; no bundle was written. Choose --token-overflow-policy warn or select to continue.");
        }

        if (candidateTokens > budget.MaxTokens && budget.OverflowPolicy == TokenOverflowPolicy.Warn)
        {
            Console.Error.WriteLine($"Warning: estimated source usage ({candidateTokens:N0} tokens) exceeds --max-tokens {budget.MaxTokens:N0}; all eligible files will be included.");
        }
        else if (candidateTokens > budget.MaxTokens && budget.OverflowPolicy == TokenOverflowPolicy.Select)
        {
            var candidates = files.Where(f => !f.IsSkipped);
            candidates = strategy switch
            {
                TokenSelectionStrategy.LargestFirst => candidates.OrderByDescending(f => f.EstimatedTokens)
                    .ThenBy(f => f.Entry.RelativePath, StringComparer.Ordinal),
                TokenSelectionStrategy.PathOrder => candidates.OrderBy(f => f.Entry.RelativePath, StringComparer.Ordinal),
                _ => throw new ArgumentOutOfRangeException(nameof(budget), $"Unknown token selection strategy '{strategy}'.")
            };

            var selectedEntries = new HashSet<BundleEntry>();
            selectedTokens = 0;
            foreach (var file in candidates)
            {
                var estimate = file.EstimatedTokens ?? 0;
                if (estimate <= budget.MaxTokens - selectedTokens)
                {
                    selectedEntries.Add(file.Entry);
                    selectedTokens += estimate;
                }
            }

            selected = files.Select(file => !file.IsSkipped && !selectedEntries.Contains(file.Entry)
                ? file with { Content = null, IsSkipped = true, SkipReason = $"omitted by token budget ({budget.MaxTokens} tokens; strategy: {strategy.ToCliValue()})" }
                : file).ToList();
        }

        PrintTokenReport(files, candidateTokens, selectedTokens);
        return new TokenBudgetResult(selected, new TokenBudgetSummary(budget.MaxTokens, budget.OverflowPolicy, strategy, candidateTokens, selectedTokens, TokenEstimator.Method));
    }

    private void PrintTokenReport(List<ProcessedFileContext> files, int candidateTokens, int selectedTokens)
    {
        Console.WriteLine($"Token estimate method: {TokenEstimator.Method}");
        foreach (var file in files.OrderBy(f => f.Entry.RelativePath, StringComparer.Ordinal))
        {
            var estimate = file.EstimatedTokens.HasValue ? $"{file.EstimatedTokens.Value:N0}" : "n/a (skipped before reading)";
            Console.WriteLine($"Tokens: {estimate,-28} {file.Entry.RelativePath}");
        }
        Console.WriteLine($"Estimated source tokens: {candidateTokens:N0}; selected: {selectedTokens:N0}; budget: {budget.MaxTokens:N0}; policy: {budget.OverflowPolicy}; strategy: {budget.SelectionStrategy.ToCliValue()}.");
        Console.WriteLine("Note: estimates cover readable file contents after optional minification, not bundle headings, tree/list metadata, or tokenizer-specific overhead.");
    }
}
