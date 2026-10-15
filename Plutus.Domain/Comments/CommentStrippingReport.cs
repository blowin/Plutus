using System.Collections.Concurrent;

namespace Plutus.Domain.Comments;

/// <summary>
/// Collects what comment processing actually did during a run: which strategy answered for each language, how many
/// files it saw, and how many comments of each kind it removed. The host prints <see cref="Snapshot"/>, which is
/// what makes "lexical scanning" versus "ANTLR token channels" a fact about the run instead of a claim in the docs.
/// </summary>
public sealed class CommentStrippingReport(CommentStrippingPlan plan) : ICommentStripObserver
{
    private readonly ConcurrentDictionary<LanguageId, LanguageStatistics> _languages = new();
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

    public void OnCommentsStripped(LanguageId language, CommentStripResult result)
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

        _languages.GetOrAdd(language, static _ => new LanguageStatistics()).Record(result);
        if (result.FallbackReason is { } reason)
        {
            _reasons.Enqueue($"{language}: {reason}");
        }
    }

    /// <summary>Captures the report as data, so a host can render it without reaching into mutable counters.</summary>
    public CommentStrippingSummary Snapshot() => new()
    {
        Request = Plan.Request,
        Strategy = Plan.DefaultStrategy,
        Rationale = Plan.Rationale,
        Files = Interlocked.Read(ref _files),
        RemovedInline = Interlocked.Read(ref _removedInline),
        RemovedBlock = Interlocked.Read(ref _removedBlock),
        RemovedDocumentation = Interlocked.Read(ref _removedDocumentation),
        PreservedDocumentation = Interlocked.Read(ref _preservedDocumentation),
        Unterminated = Interlocked.Read(ref _unterminated),
        Fallbacks = Interlocked.Read(ref _fallbacks),
        Languages = _languages.ToDictionary(pair => pair.Key, pair => pair.Value.Files),
        Strategies = _languages.ToDictionary(pair => pair.Key, pair => pair.Value.Strategy),
        FallbackReasons = _reasons.ToArray(),
    };

    private sealed class LanguageStatistics
    {
        private long _files;
        private int _strategy = (int)CommentStripStrategy.None;

        public long Files => Interlocked.Read(ref _files);

        public CommentStripStrategy Strategy => (CommentStripStrategy)Volatile.Read(ref _strategy);

        public void Record(CommentStripResult result)
        {
            Interlocked.Increment(ref _files);
            Volatile.Write(ref _strategy, (int)result.Strategy);
        }
    }
}
