namespace Plutus.Domain.IgnoreMatcher;

public class IgnoreMatcherBuilder
{
    private readonly CollectionIgnoreMatcher _ignoreMatcher = new();

    public IgnoreMatcherBuilder IgnoreJunk() => Add(new JunkDirectoryIgnoreMatcher());

    public IgnoreMatcherBuilder IgnoreDangerousFile() => Add(new DangerousFileIgnoreMatcher());

    public IgnoreMatcherBuilder Add(IIgnoreMatcher matcher)
    {
        _ignoreMatcher.Add(matcher);
        return this;
    }

    public IIgnoreMatcher Build() => _ignoreMatcher;

    private class CollectionIgnoreMatcher : IIgnoreMatcher
    {
        private readonly Dictionary<Type, IIgnoreMatcher> _matchers = [];

        public void Add(IIgnoreMatcher matcher) => _matchers.TryAdd(matcher.GetType(), matcher);

        public bool IsIgnoredFile(string relativePath) => _matchers.Count != 0 && _matchers.Any(e => e.Value.IsIgnoredFile(relativePath));

        public bool IsIgnoredDirectory(string relativePath) => _matchers.Count != 0 && _matchers.Any(e => e.Value.IsIgnoredDirectory(relativePath));
    }

}
