namespace Plutus.Domain.Tokens.Applier;

/// <summary>One budgetable unit: a named piece of text with its token estimate.</summary>
public sealed record TokenBudgetRequest(string Path, int EstimatedTokens);

/// <summary>Outcome of the selection. <see cref="Selected"/> is null when the budget was not exceeded.</summary>
public sealed record TokenBudgetDecision(
    IReadOnlySet<string>? Selected,
    long CandidateTokens,
    long SelectedTokens);

/// <summary>
/// The whole-file selection rules of the token budget, independent of what is being budgeted. Content files
/// and per-file diff chunks both narrow a bundle to a token ceiling, and both must apply the same policy and
/// the same deterministic strategy, so the rules live here once.
/// </summary>
public sealed class TokenBudgetSelector(TokenBudgetOptions budget)
{
    public bool IsExceeded(IReadOnlyList<TokenBudgetRequest> requests) =>
        Sum(requests) > budget.MaxTokens;

    public long Sum(IReadOnlyList<TokenBudgetRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        // Sum as long: a large repository can exceed Int32 even though each estimate is an int.
        return requests.Sum(request => (long)request.EstimatedTokens);
    }

    public TokenBudgetDecision Select(IReadOnlyList<TokenBudgetRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var candidateTokens = Sum(requests);
        if (candidateTokens <= budget.MaxTokens)
        {
            return new TokenBudgetDecision(null, candidateTokens, candidateTokens);
        }

        var ordered = budget.SelectionStrategy switch
        {
            TokenSelectionStrategy.LargestFirst => requests.OrderByDescending(r => r.EstimatedTokens)
                .ThenBy(r => r.Path, StringComparer.Ordinal),
            TokenSelectionStrategy.PathOrder => requests.OrderBy(r => r.Path, StringComparer.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(budget), $"Unknown token selection strategy '{budget.SelectionStrategy}'.")
        };

        var selected = new HashSet<string>(StringComparer.Ordinal);
        long selectedTokens = 0;
        foreach (var request in ordered)
        {
            if (request.EstimatedTokens <= budget.MaxTokens - selectedTokens)
            {
                selected.Add(request.Path);
                selectedTokens += request.EstimatedTokens;
            }
        }

        return new TokenBudgetDecision(selected, candidateTokens, selectedTokens);
    }

    /// <summary>Skip reason recorded on units the <c>select</c> policy left out.</summary>
    public string OmittedReason =>
        $"omitted by token budget ({budget.MaxTokens} tokens; strategy: {budget.SelectionStrategy})";
}
