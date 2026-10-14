using Plutus.Domain.BundleWriters;

namespace Plutus.Domain.Tokens.Applier;

public sealed record TokenBudgetResult(
    List<ProcessedFileContext> Files,
    TokenBudgetSummary? Summary);

/// <summary>Applies an optional token budget to already-processed source files.</summary>
public interface ITokenBudgetApplier
{
    bool IsEnabled { get; }
    TokenBudgetResult Apply(IReadOnlyList<ProcessedFileContext> files);
}
