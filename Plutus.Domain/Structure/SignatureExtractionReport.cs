using System.Collections.Concurrent;

namespace Plutus.Domain.Structure;

/// <summary>
/// Collects what structural extraction actually did during a run: which provider answered, how many bodies of each
/// kind were cut, and how often the parser disagreed with the file. The CLI prints this, which is what keeps
/// "signature extraction" from being an unverifiable claim about the bundle.
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

    public long FilesProcessed => Interlocked.Read(ref _files);

    public long StrippedBodies => _bodiesByKind.Values.Sum(counter => counter.Value);

    public long UnsupportedFiles => Interlocked.Read(ref _unsupportedFiles);

    public long FilesWithParseDiagnostics => Interlocked.Read(ref _filesWithDiagnostics);

    public long ParseDiagnostics => Interlocked.Read(ref _parseDiagnostics);

    public long Fallbacks => Interlocked.Read(ref _fallbacks);

    /// <summary>Provider names actually used, with the number of files each answered for.</summary>
    public IReadOnlyDictionary<string, long> ProvidersUsed =>
        _languages.Values
            .Where(statistics => statistics.Provider is not null)
            .GroupBy(statistics => statistics.Provider!)
            .ToDictionary(group => group.Key, group => group.Sum(statistics => statistics.Files), StringComparer.Ordinal);

    public IReadOnlyCollection<string> FallbackReasons => _reasons.ToArray();

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

    /// <summary>Counts of stripped bodies per declaration kind, ordered by kind for a stable report.</summary>
    public IReadOnlyDictionary<MemberKind, long> StrippedBodiesByKind =>
        _bodiesByKind.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value.Value);

    public string Describe()
    {
        if (!IsActive)
        {
            return "Signature extraction: no source file was processed structurally";
        }

        var providers = ProvidersUsed.Count == 0
            ? "none"
            : string.Join(", ", ProvidersUsed.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));

        var kinds = _bodiesByKind.Count == 0
            ? "none"
            : string.Join(", ", _bodiesByKind.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key.ToString().ToLowerInvariant()} {pair.Value.Value}"));

        return $"Signature extraction: files={FilesProcessed}; bodies stripped={StrippedBodies} ({kinds}); "
               + $"providers: {providers}; without a provider={UnsupportedFiles}; "
               + $"files with parse diagnostics={FilesWithParseDiagnostics} ({ParseDiagnostics} diagnostic(s)); fallbacks={Fallbacks}";
    }

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
