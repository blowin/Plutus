using Plutus.Domain.BundleWriters;

namespace Plutus.Domain.Tokens.Applier;

/// <summary>Leaves the bundle unchanged when no --max-tokens configuration was supplied.</summary>
public sealed class NoOpTokenBudgetApplier : ITokenBudgetApplier
{
    public bool IsEnabled => false;
    public TokenBudgetResult Apply(IReadOnlyList<ProcessedFileContext> files) =>
        new(files.ToList(), null);
}
