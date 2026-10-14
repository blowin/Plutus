namespace Plutus.Domain.Tokens;

/// <summary>Controls source-file selection when a bundle has a target token budget.</summary>
public sealed record TokenBudgetOptions(
    int MaxTokens,
    TokenOverflowPolicy OverflowPolicy = TokenOverflowPolicy.Fail,
    TokenSelectionStrategy SelectionStrategy = TokenSelectionStrategy.PathOrder);

public enum TokenOverflowPolicy
{
    Fail,
    Warn,
    Select
}

public enum TokenSelectionStrategy
{
    PathOrder,
    LargestFirst
}

public sealed record TokenBudgetSummary(
    int MaxTokens,
    TokenOverflowPolicy OverflowPolicy,
    TokenSelectionStrategy SelectionStrategy,
    long CandidateTokens,
    long SelectedTokens,
    string EstimatorMethod);

public sealed class TokenBudgetExceededException(string message) : Exception(message);
