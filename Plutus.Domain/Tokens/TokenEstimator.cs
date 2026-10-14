namespace Plutus.Domain.Tokens;

/// <summary>Offline heuristic, not an exact model-tokenizer result.</summary>
public static class TokenEstimator
{
    public const string Method = "chars/4-v1 (Unicode scalar values; source content only; rounded up per file)";

    public static int Estimate(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var scalarCount = 0;
        foreach (var _ in content.EnumerateRunes())
        {
            scalarCount++;
        }

        return scalarCount == 0 ? 0 : checked((scalarCount + 3) / 4);
    }
}
