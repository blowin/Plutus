namespace Plutus.Domain.BundleWriters;

public sealed record ProcessedFileContext(
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
