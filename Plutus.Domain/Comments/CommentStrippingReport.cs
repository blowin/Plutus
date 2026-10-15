using System.Collections.Concurrent;

namespace Plutus.Domain.Comments;

/// <summary>
/// Collects what comment processing actually did during a run: which strategy answered for each language, how
/// many files it saw, and how many comments of each kind it removed. The CLI prints this, which is what makes
/// "lexical scanning" versus "ANTLR token channels" a fact about the run instead of a claim in the docs.
/// </summary>
public sealed class CommentStrippingReport(CommentStrippingPlan plan) : ICommentStripObserver
{
    private readonly ConcurrentDictionary<string, LanguageStatistics> _languages = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _reasons = new();
    private long _files;
    private long _removedInline;
    private long _removedBlock;
    private long _removedDocumentation;
    private long _preservedDocumentation;
    private long _unterminated;
    private long _fallbacks;

    public CommentStrippingPlan Plan { get; } = plan;

    /// <summary>True once at least one file went through a comment stripper.</summary>
    public bool IsActive => Interlocked.Read(ref _files) > 0;

    public long FilesProcessed => Interlocked.Read(ref _files);

    public long RemovedComments => Interlocked.Read(ref _removedInline) + Interlocked.Read(ref _removedBlock) + Interlocked.Read(ref _removedDocumentation);

    public long PreservedDocumentationComments => Interlocked.Read(ref _preservedDocumentation);

    public long UnterminatedSequences => Interlocked.Read(ref _unterminated);

    public long Fallbacks => Interlocked.Read(ref _fallbacks);

    /// <summary>Languages actually processed, with the strategy that answered for each.</summary>
    public IReadOnlyDictionary<string, string> StrategiesByLanguage =>
        _languages.ToDictionary(pair => pair.Key, pair => pair.Value.Strategy.ToString(), StringComparer.OrdinalIgnoreCase);

    public void OnCommentsStripped(string language, CommentStripResult result)
    {
        Interlocked.Increment(ref _files);
        Interlocked.Add(ref _removedInline, result.RemovedInlineComments);
        Interlocked.Add(ref _removedBlock, result.RemovedBlockComments);
        Interlocked.Add(ref _removedDocumentation, result.RemovedDocumentationComments);
        Interlocked.Add(ref _preservedDocumentation, result.PreservedDocumentationComments);
        Interlocked.Add(ref _unterminated, result.UnterminatedSequences);
        if (result.FallbackReason is not null)
        {
            Interlocked.Increment(ref _fallbacks);
        }

        var statistics = _languages.GetOrAdd(language, static _ => new LanguageStatistics());
        statistics.Record(result);
        if (result.FallbackReason is { } reason)
        {
            _reasons.Enqueue($"{language}: {reason}");
        }
    }

    /// <summary>Strategies that declined to transform a file, with the reason. Empty when nothing fell back.</summary>
    public IReadOnlyCollection<string> FallbackReasons => _reasons.ToArray();

    /// <summary>One-line-per-fact summary of the run, with the requested and effective strategy spelled out.</summary>
    public string Describe()
    {
        var strategy = $"Comment stripping: {Plan.Describe()}";
        if (!IsActive)
        {
            return $"{strategy}; no source file required comment processing";
        }

        var languages = string.Join(", ", _languages
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}={pair.Value.Files}"));
        var fallbacks = Fallbacks == 0 ? string.Empty : $"; strategy fallbacks={Fallbacks}";

        return $"{strategy}\n"
               + $"Comment stripping: files={FilesProcessed}; removed={RemovedComments} "
               + $"(inline {Interlocked.Read(ref _removedInline)}, block {Interlocked.Read(ref _removedBlock)}, documentation {Interlocked.Read(ref _removedDocumentation)}); "
               + $"documentation preserved={PreservedDocumentationComments}; unterminated={UnterminatedSequences}{fallbacks}; languages: {languages}";
    }

    private sealed class LanguageStatistics
    {
        private long _files;
        private string _strategy = "None";

        public long Files => Interlocked.Read(ref _files);

        public string Strategy => Volatile.Read(ref _strategy);

        public void Record(CommentStripResult result)
        {
            Interlocked.Increment(ref _files);
            Volatile.Write(ref _strategy, result.Strategy.ToString());
        }
    }
}
