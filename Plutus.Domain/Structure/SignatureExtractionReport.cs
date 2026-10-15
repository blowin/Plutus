using System.Collections.Concurrent;

namespace Plutus.Domain.Structure;

/// <summary>
/// Collects what structural extraction actually did during a run: which provider answered, how many bodies of each
/// kind were cut, and how often the parser disagreed with the file. The host prints <see cref="Snapshot"/>, which is
/// what keeps "signature extraction" from being an unverifiable claim about the bundle.
/// </summary>
public sealed class SignatureExtractionReport : ISignatureExtractionObserver
{
    private readonly ConcurrentDictionary<LanguageId, LanguageStatistics> _languages = new();
    private readonly ConcurrentDictionary<MemberKind, Counter> _bodiesByKind = new();
    private readonly ConcurrentQueue<string> _reasons = new();
    private long _files;
    private long _unsupportedFiles;
    private long _filesWithDiagnostics;
    private long _parseDiagnostics;
    private long _fallbacks;

    public bool IsActive => Interlocked.Read(ref _files) > 0;

    public void OnSignaturesExtracted(LanguageId language, SignatureExtractionResult result)
    {
        Interlocked.Increment(ref _files);
        Interlocked.Add(ref _parseDiagnostics, result.ParseDiagnostics);
        if (result.ParseDiagnostics > 0)
        {
            Interlocked.Increment(ref _filesWithDiagnostics);
        }

        if (!result.LanguageSupported)
        {
            // A language without a provider is expected, reported as such, and not a fallback.
            Interlocked.Increment(ref _unsupportedFiles);
        }
        else if (result.FallbackReason is { } reason)
        {
            Interlocked.Increment(ref _fallbacks);
            _reasons.Enqueue($"{language}: {reason}");
        }

        foreach (var (kind, count) in result.StrippedBodies)
        {
            _bodiesByKind.GetOrAdd(kind, static _ => new Counter()).Add(count);
        }

        _languages.GetOrAdd(language, static _ => new LanguageStatistics()).Record(result.Provider);
    }

    /// <summary>Captures the report as data, so a host can render it without reaching into mutable counters.</summary>
    public SignatureExtractionSummary Snapshot() => new()
    {
        Files = Interlocked.Read(ref _files),
        BodiesByKind = _bodiesByKind.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value.Value),
        Providers = _languages.Values
            .Where(statistics => statistics.Provider is not null)
            .GroupBy(statistics => statistics.Provider!)
            .ToDictionary(group => group.Key, group => (long)group.Sum(statistics => statistics.Files), StringComparer.Ordinal),
        FilesWithoutProvider = Interlocked.Read(ref _unsupportedFiles),
        FilesWithParseDiagnostics = Interlocked.Read(ref _filesWithDiagnostics),
        ParseDiagnostics = Interlocked.Read(ref _parseDiagnostics),
        Fallbacks = Interlocked.Read(ref _fallbacks),
        FallbackReasons = _reasons.ToArray(),
    };

    private sealed class Counter
    {
        private long _value;

        public long Value => Interlocked.Read(ref _value);

        public void Add(int count) => Interlocked.Add(ref _value, count);
    }

    private sealed class LanguageStatistics
    {
        private long _files;
        private string? _provider;

        public long Files => Interlocked.Read(ref _files);

        public string? Provider => Volatile.Read(ref _provider);

        public void Record(string? provider)
        {
            Interlocked.Increment(ref _files);
            if (provider is not null)
            {
                Volatile.Write(ref _provider, provider);
            }
        }
    }
}
