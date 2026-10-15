namespace Plutus.Domain;

/// <summary>One limitable payload: a name, its size on disk, and its token estimate.</summary>
public sealed record BundleLimitItem(string Key, string DisplayPath, long SizeInBytes, int EstimatedTokens);

/// <summary>A payload the limits excluded, with the reason the bundle reports in its place.</summary>
public sealed record BundleLimitExclusion(string Key, string DisplayPath, string Reason);

/// <summary>
/// Outcome of applying the limits: what stays, what was excluded and why, and the budget numbers.
/// </summary>
public sealed record BundleLimitResult(
    IReadOnlyList<string> IncludedKeys,
    IReadOnlyList<BundleLimitExclusion> Exclusions,
    long CandidateTokens,
    long SelectedTokens);

/// <summary>
/// The single implementation of every bundle limit. Both payloads the bundle can carry — source files and
/// per-file diffs — are reduced to <see cref="BundleLimitItem"/> and passed through here, so the size rule,
/// the token budget, the overflow policies and the selection strategies exist in exactly one place and
/// cannot drift apart between them.
/// </summary>
public sealed class BundleLimits(FileSize maxFileSize, Tokens.TokenBudgetOptions? tokenBudget)
{
    public bool ExcludedBySize(long sizeInBytes) => sizeInBytes > maxFileSize;

    public string SizeExclusionReason(long sizeInBytes) =>
        $"file is larger than max-file-size ({sizeInBytes.HumanSize()} > {maxFileSize})";

    /// <summary>
    /// Applies the size rule first, then the token budget to whatever survived, exactly as the content path
    /// has always done. An item is reported as excluded rather than dropped, so the caller can say so.
    /// </summary>
    public BundleLimitResult Apply(IReadOnlyList<BundleLimitItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var exclusions = new List<BundleLimitExclusion>();
        var eligible = new List<BundleLimitItem>(items.Count);
        foreach (var item in items)
        {
            if (ExcludedBySize(item.SizeInBytes))
            {
                exclusions.Add(new BundleLimitExclusion(item.Key, item.DisplayPath, SizeExclusionReason(item.SizeInBytes)));
                continue;
            }

            eligible.Add(item);
        }

        if (tokenBudget is null)
        {
            return new BundleLimitResult(
                eligible.Select(item => item.Key).ToArray(),
                exclusions,
                eligible.Sum(item => (long)item.EstimatedTokens),
                eligible.Sum(item => (long)item.EstimatedTokens));
        }

        var selector = new Tokens.Applier.TokenBudgetSelector(tokenBudget);
        var requests = eligible.Select(item => new Tokens.Applier.TokenBudgetRequest(item.Key, item.EstimatedTokens)).ToArray();
        var candidateTokens = selector.Sum(requests);

        if (!selector.IsExceeded(requests))
        {
            return new BundleLimitResult(eligible.Select(item => item.Key).ToArray(), exclusions, candidateTokens, candidateTokens);
        }

        if (tokenBudget.OverflowPolicy == Tokens.TokenOverflowPolicy.Fail)
        {
            throw new Tokens.TokenBudgetExceededException(
                $"Estimated usage ({candidateTokens:N0} tokens) exceeds --max-tokens {tokenBudget.MaxTokens:N0}; no bundle was written. Choose --token-overflow-policy warn or select to continue.");
        }

        if (tokenBudget.OverflowPolicy == Tokens.TokenOverflowPolicy.Warn)
        {
            Console.Error.WriteLine($"Warning: estimated usage ({candidateTokens:N0} tokens) exceeds --max-tokens {tokenBudget.MaxTokens:N0}; all eligible items will be included.");
            return new BundleLimitResult(eligible.Select(item => item.Key).ToArray(), exclusions, candidateTokens, candidateTokens);
        }

        var decision = selector.Select(requests);
        var selected = decision.Selected ?? new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in eligible.Where(item => !selected.Contains(item.Key)))
        {
            exclusions.Add(new BundleLimitExclusion(item.Key, item.DisplayPath, selector.OmittedReason));
        }

        return new BundleLimitResult(
            eligible.Where(item => selected.Contains(item.Key)).Select(item => item.Key).ToArray(),
            exclusions,
            candidateTokens,
            decision.SelectedTokens);
    }

    /// <summary>Summary for the bundle header, or <c>null</c> when no budget was configured.</summary>
    public Tokens.TokenBudgetSummary? CreateSummary(BundleLimitResult result) =>
        tokenBudget is null
            ? null
            : new Tokens.TokenBudgetSummary(
                tokenBudget.MaxTokens,
                tokenBudget.OverflowPolicy,
                tokenBudget.SelectionStrategy,
                result.CandidateTokens,
                result.SelectedTokens,
                Tokens.TokenEstimator.Method);
}
