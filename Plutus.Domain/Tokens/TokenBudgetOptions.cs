namespace Plutus.Domain.Tokens;

/// <summary>Controls source-file selection when a bundle has a target token budget.</summary>
public sealed record TokenBudgetOptions(int MaxTokens, string OverflowPolicy = "fail", string SelectionStrategy = "path-order");

public sealed record TokenBudgetSummary(
    int MaxTokens,
    string OverflowPolicy,
    string SelectionStrategy,
    int CandidateTokens,
    int SelectedTokens,
    string EstimatorMethod);

public sealed class TokenBudgetExceededException(string message) : Exception(message);
