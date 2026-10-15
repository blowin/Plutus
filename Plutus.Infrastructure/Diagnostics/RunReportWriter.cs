using System.Globalization;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Structure;

namespace Plutus.Infrastructure.Diagnostics;

/// <summary>Prints the report of the run that just finished.</summary>
public interface IRunReportWriter
{
    void Write();
}

/// <summary>
/// Renders the run report: what the bundle holds, what source processing did, and what a pass could not do. All
/// run-level presentation lives here, in one place, so the host only decides whether a report is wanted — and so the
/// wording, the alignment and the culture of the numbers cannot drift between callers.
/// <para>
/// The writer is a service: it takes the output, the options and the reports of the run, and a host asks it to
/// print. A test builds the same object with a recording output and summary data, without a container.
/// </para>
/// </summary>
public sealed class RunReportWriter : IRunReportWriter
{
    private const int Indent = 2;
    private const int LabelGap = 2;

    private readonly IRunOutput _output;
    private readonly RunAppOptions _options;
    private readonly BundleRunReport _bundle;
    private readonly Func<CommentStrippingSummary> _comments;
    private readonly Func<SignatureExtractionSummary> _signatures;

    /// <summary>Wires the writer to the reports of a run, which are read when the report is printed.</summary>
    public RunReportWriter(
        IRunOutput output,
        RunAppOptions options,
        BundleRunReport bundle,
        CommentStrippingReport comments,
        SignatureExtractionReport signatures)
        : this(output, options, bundle, comments.Snapshot, signatures.Snapshot)
    {
    }

    /// <summary>Wires the writer straight to the data, which is what a test or a library host has.</summary>
    public RunReportWriter(
        IRunOutput output,
        RunAppOptions options,
        BundleRunReport bundle,
        CommentStrippingSummary comments,
        SignatureExtractionSummary signatures)
        : this(output, options, bundle, () => comments, () => signatures)
    {
    }

    private RunReportWriter(
        IRunOutput output,
        RunAppOptions options,
        BundleRunReport bundle,
        Func<CommentStrippingSummary> comments,
        Func<SignatureExtractionSummary> signatures)
    {
        _output = output;
        _options = options;
        _bundle = bundle;
        _comments = comments;
        _signatures = signatures;
    }

    public void Write()
    {
        WriteConfiguration();
        WriteBundle();
        WriteSourceProcessing(_comments(), _signatures());
    }

    /// <summary>
    /// Which profile configured the run, which of its keys decided anything, and which the command line replaced. A
    /// preset that stopped applying is otherwise invisible, and a silent preset is worse than a long command line.
    /// Keys the profile restates at their default value are counted, not listed: they are not what decided the run.
    /// </summary>
    private void WriteConfiguration()
    {
        if (_options.Profile is not { } profile)
        {
            return;
        }

        var parts = new List<string>();
        if (profile.AppliedKeys.Count > 0)
        {
            parts.Add($"applied: {string.Join(", ", profile.AppliedKeys)}");
        }

        if (profile.DefaultedKeys.Count > 0)
        {
            parts.Add($"restated defaults: {profile.DefaultedKeys.Count}");
        }

        if (profile.OverriddenKeys.Count > 0)
        {
            parts.Add($"overridden by the command line: {string.Join(", ", profile.OverriddenKeys)}");
        }

        _output.Info(string.Empty);
        _output.Info("Configuration");
        _output.Info($"  profile  {profile.Path}{(parts.Count == 0 ? " — no key was used" : $" — {string.Join("; ", parts)}")}");
    }

    private void WriteBundle()
    {
        if (!_bundle.HasResult)
        {
            return;
        }

        var rows = new (string Label, string Value)[]
        {
            ("Bundle created:", _bundle.OutputPath!),
            ("Files included:", Number(_bundle.FilesIncluded)),
            ("Total size:", _bundle.TotalBytes.HumanSize()),
            ("Elapsed time:", $"{_bundle.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s ({Number(_bundle.Elapsed.TotalMilliseconds)} ms)"),
        };

        var width = rows.Max(row => row.Label.Length);
        foreach (var (label, value) in rows)
        {
            _output.Info($"{label.PadRight(width)}{new string(' ', LabelGap)}{value}");
        }
    }

    private void WriteSourceProcessing(CommentStrippingSummary comments, SignatureExtractionSummary signatures)
    {
        var commentRequested = _options.CommentStripPlan is not null;
        var signaturesRequested = _options.SignaturesOnly;

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
        _output.Info(string.Empty);
        _output.Info("Source processing");
        foreach (var (label, value) in lines)
        {
            var prefix = new string(' ', Indent) + label.PadRight(width) + new string(' ', LabelGap);
            _output.Info($"{prefix}{value}");
        }

        ReportFallbacks(comments, signatures);
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

    private void ReportFallbacks(CommentStrippingSummary comments, SignatureExtractionSummary signatures)
    {
        foreach (var reason in comments.FallbackReasons)
        {
            _output.Warning($"comment stripping did not run for {reason}");
        }

        foreach (var reason in signatures.FallbackReasons)
        {
            _output.Warning($"signature extraction did not run for {reason}");
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
