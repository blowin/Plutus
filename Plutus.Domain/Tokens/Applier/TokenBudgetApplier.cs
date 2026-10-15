using Plutus.Domain.BundleWriters;

namespace Plutus.Domain.Tokens.Applier;

/// <summary>Applies deterministic whole-file token-budget policies.</summary>
public sealed class TokenBudgetApplier(TokenBudgetOptions budget) : ITokenBudgetApplier
{
    private static readonly IReadOnlySet<string> EmptySelection = new HashSet<string>(StringComparer.Ordinal);

    private readonly TokenBudgetSelector _selector = new(budget);

    public bool IsEnabled => true;

    public TokenBudgetResult Apply(IReadOnlyList<ProcessedFileContext> sourceFiles)
    {
        if (budget.MaxTokens < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), "Max tokens must be at least 1.");
        }

        if (!Enum.IsDefined(budget.OverflowPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(budget), $"Unknown token overflow policy '{budget.OverflowPolicy}'.");
        }

        var files = sourceFiles.ToList();
        var candidates = files.Where(f => !f.IsSkipped)
            .Select(f => new TokenBudgetRequest(f.Entry.RelativePath, f.EstimatedTokens ?? 0))
            .ToList();

        var candidateTokens = _selector.Sum(candidates);
        var selected = files;
        var selectedTokens = candidateTokens;

        if (candidateTokens > budget.MaxTokens && budget.OverflowPolicy == TokenOverflowPolicy.Fail)
        {
            PrintTokenReport(files, [], candidateTokens, 0, failPolicy: true);
            throw new TokenBudgetExceededException($"Estimated source usage ({candidateTokens:N0} tokens) exceeds --max-tokens {budget.MaxTokens:N0}; no bundle was written. Choose --token-overflow-policy warn or select to continue.");
        }

        if (candidateTokens > budget.MaxTokens && budget.OverflowPolicy == TokenOverflowPolicy.Warn)
        {
            Console.Error.WriteLine($"Warning: estimated source usage ({candidateTokens:N0} tokens) exceeds --max-tokens {budget.MaxTokens:N0}; all eligible files will be included.");
        }
        else if (candidateTokens > budget.MaxTokens && budget.OverflowPolicy == TokenOverflowPolicy.Select)
        {
            var decision = _selector.Select(candidates);
            selectedTokens = decision.SelectedTokens;
            var selectedPaths = decision.Selected ?? EmptySelection;

            selected = files.ConvertAll(file => !file.IsSkipped && !selectedPaths.Contains(file.Entry.RelativePath)
                ? file with
                {
                    Content = null,
                    IsSkipped = true,
                    IsTokenBudgetOmitted = true,
                    SkipReason = _selector.OmittedReason
                }
                : file);
        }

        if (candidateTokens > budget.MaxTokens)
        {
            PrintTokenReport(files, selected, candidateTokens, selectedTokens, failPolicy: false);
        }

        return new TokenBudgetResult(selected, new TokenBudgetSummary(budget.MaxTokens, budget.OverflowPolicy, budget.SelectionStrategy, candidateTokens, selectedTokens, TokenEstimator.Method));
    }

    private void PrintTokenReport(
        IReadOnlyCollection<ProcessedFileContext> sourceFiles,
        IReadOnlyCollection<ProcessedFileContext> selectedFiles,
        long candidateTokens,
        long selectedTokens,
        bool failPolicy)
    {
        var selectedPaths = selectedFiles
            .Where(f => !f.IsSkipped)
            .Select(f => f.Entry.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        Console.WriteLine($"Token estimate method: {TokenEstimator.Method}");
        foreach (var file in sourceFiles.OrderBy(f => f.Entry.RelativePath, StringComparer.Ordinal))
        {
            var estimate = file.EstimatedTokens.HasValue
                ? $"{file.EstimatedTokens.Value:N0}"
                : "n/a (skipped before reading)";
            var decision = file.IsTokenBudgetOmitted
                ? "omitted by budget"
                : file.IsSkipped
                    ? $"skipped: {file.SkipReason ?? "unknown reason"}"
                    : failPolicy
                        ? "not written (fail policy)"
                        : selectedPaths.Contains(file.Entry.RelativePath)
                            ? "included"
                            : "omitted";

            Console.WriteLine($"{estimate,-28} {decision,-28} {file.Entry.RelativePath}");
        }

        Console.WriteLine($"Estimated source tokens: {candidateTokens:N0}; selected: {selectedTokens:N0}; budget: {budget.MaxTokens:N0}; policy: {budget.OverflowPolicy.ToString().ToLowerInvariant()}; strategy: {budget.SelectionStrategy}.");
        Console.WriteLine("Estimates cover readable source content after optional minification, not bundle metadata or tokenizer-specific overhead.");
    }
}
