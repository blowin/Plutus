using System.Globalization;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Structure;

namespace Plutus.Infrastructure.Diagnostics;

/// <summary>
/// Renders the run report: what the bundle holds, what source processing did, and what a pass could not do. All
/// run-level presentation lives here, in one place, so the host only decides whether a report is wanted — and so the
/// wording, the alignment and the culture of the numbers cannot drift between callers.
/// </summary>
public static class RunReportWriter
{
    private const int Indent = 2;
    private const int LabelGap = 2;

    public static void Write(
        IRunOutput output,
        RunAppOptions options,
        BundleRunReport bundle,
        CommentStrippingSummary comments,
        SignatureExtractionSummary signatures)
    {
        WriteBundle(output, bundle);
        WriteSourceProcessing(output, options, comments, signatures);
    }

    private static void WriteBundle(IRunOutput output, BundleRunReport bundle)
    {
        if (!bundle.HasResult)
        {
            return;
        }

        var rows = new (string Label, string Value)[]
        {
            ("Bundle created:", bundle.OutputPath!),
            ("Files included:", Number(bundle.FilesIncluded)),
            ("Total size:", bundle.TotalBytes.HumanSize()),
            ("Elapsed time:", $"{bundle.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s ({Number(bundle.Elapsed.TotalMilliseconds)} ms)"),
        };

        var width = rows.Max(row => row.Label.Length);
        foreach (var (label, value) in rows)
        {
            output.Info($"{label.PadRight(width)}{new string(' ', LabelGap)}{value}");
        }
    }

    private static void WriteSourceProcessing(
        IRunOutput output,
        RunAppOptions options,
        CommentStrippingSummary comments,
        SignatureExtractionSummary signatures)
    {
        var commentRequested = options.CommentStripPlan is not null;
        var signaturesRequested = options.SignaturesOnly;

        if (!commentRequested && !signaturesRequested)
        {
            return;
        }

        var lines = new List<(string Label, string Value)>();
        if (commentRequested)
        {
            lines.Add(("comments", DescribeStrategy(comments)));
            lines.Add((string.Empty, DescribeCommentCounts(comments)));
        }

        if (signaturesRequested)
        {
            lines.Add(("signatures", DescribeProviders(signatures)));
            lines.Add((string.Empty, DescribeSignatureCounts(signatures)));
        }

        var width = lines.Max(line => line.Label.Length);
        output.Info(string.Empty);
        output.Info("Source processing");
        foreach (var (label, value) in lines)
        {
            var prefix = new string(' ', Indent) + label.PadRight(width) + new string(' ', LabelGap);
            output.Info($"{prefix}{value}");
        }

        ReportFallbacks(output, comments, signatures);
    }

    private static string DescribeStrategy(CommentStrippingSummary summary)
    {
        var strategy = $"strategy={summary.Strategy.ToDisplayName()} (requested: {summary.Request.ToDisplayName()})";
        return string.IsNullOrWhiteSpace(summary.Rationale) ? strategy : $"{strategy} — {summary.Rationale}";
    }

    private static string DescribeCommentCounts(CommentStrippingSummary summary)
    {
        if (!summary.IsActive)
        {
            return "no source file required comment processing";
        }

        var parts = new List<string>
        {
            $"files={Number(summary.Files)}",
            $"removed={Number(summary.RemovedComments)} (inline {Number(summary.RemovedInline)}, block {Number(summary.RemovedBlock)}, documentation {Number(summary.RemovedDocumentation)})",
        };

        AddWhenPositive(parts, "documentation preserved", summary.PreservedDocumentation);
        AddWhenPositive(parts, "unterminated", summary.Unterminated);
        AddWhenPositive(parts, "fallbacks", summary.Fallbacks);

        if (summary.Languages.Count > 0)
        {
            parts.Add($"languages: {JoinCounts(summary.Languages.ToDictionary(pair => pair.Key.Value, pair => pair.Value))}");
        }

        return string.Join(", ", parts);
    }

    private static string DescribeProviders(SignatureExtractionSummary summary)
    {
        // Provider names only: the file counts are already on the line below, and the per-provider numbers stay in
        // the summary for callers that need them.
        var names = summary.Providers.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();
        return $"provider={(names.Count == 0 ? "none" : string.Join(", ", names))}";
    }

    private static string DescribeSignatureCounts(SignatureExtractionSummary summary)
    {
        if (!summary.IsActive)
        {
            return "no source file required structural processing";
        }

        var parts = new List<string>
        {
            $"files={Number(summary.Files)}",
            $"bodies={Number(summary.StrippedBodies)}",
        };

        if (summary.BodiesByKind.Count > 0)
        {
            parts[^1] += $" ({JoinCounts(summary.BodiesByKind.ToDictionary(pair => pair.Key.ToString().ToLowerInvariant(), pair => pair.Value))})";
        }

        AddWhenPositive(parts, "without a provider", summary.FilesWithoutProvider);
        AddWhenPositive(parts, "files with parse diagnostics", summary.FilesWithParseDiagnostics);
        AddWhenPositive(parts, "fallbacks", summary.Fallbacks);

        return string.Join(", ", parts);
    }

    private static void ReportFallbacks(
        IRunOutput output,
        CommentStrippingSummary comments,
        SignatureExtractionSummary signatures)
    {
        foreach (var reason in comments.FallbackReasons)
        {
            output.Warning($"comment stripping did not run for {reason}");
        }

        foreach (var reason in signatures.FallbackReasons)
        {
            output.Warning($"signature extraction did not run for {reason}");
        }
    }

    private static void AddWhenPositive(List<string> parts, string label, long value)
    {
        if (value > 0)
        {
            parts.Add($"{label}={Number(value)}");
        }
    }

    private static string JoinCounts(IReadOnlyDictionary<string, long> counts) =>
        string.Join(", ", counts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={Number(pair.Value)}"));

    private static string Number(double value) => value.ToString("F0", CultureInfo.InvariantCulture);

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
