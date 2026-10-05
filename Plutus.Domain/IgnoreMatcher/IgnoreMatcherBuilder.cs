namespace Plutus.Domain.IgnoreMatcher;

public class IgnoreMatcherBuilder
{
    private readonly CollectionIgnoreMatcher _ignoreMatcher = new();

    public IgnoreMatcherBuilder IgnoreJunk() => Add(new JunkDirectoryIgnoreMatcher(), false);

    public IgnoreMatcherBuilder IgnoreDangerousFile() => Add(new DangerousFileIgnoreMatcher(), false);

    public IgnoreMatcherBuilder Add(IIgnoreMatcher matcher) => Add(matcher, true);

    private IgnoreMatcherBuilder Add(IIgnoreMatcher matcher, bool addIfExistsType)
    {
        _ignoreMatcher.Add(matcher, addIfExistsType);
        return this;
    }

    public IIgnoreMatcher Build() => _ignoreMatcher;

    private class CollectionIgnoreMatcher : IIgnoreMatcher
    {
        private readonly List<IIgnoreMatcher> _matchers = [];

        public void Add(IIgnoreMatcher matcher, bool addIfExistsType)
        {
            if (!addIfExistsType)
            {
                var matcherType = matcher.GetType();
                if (_matchers.Any(m => m.GetType() == matcherType))
                {
                    return;
                }
            }

            _matchers.Add(matcher);
        }

        public bool IsIgnoredFile(string relativePath) => _matchers.Count != 0 && _matchers.Any(e => e.IsIgnoredFile(relativePath));

        public bool IsIgnoredDirectory(string relativePath) => _matchers.Count != 0 && _matchers.Any(e => e.IsIgnoredDirectory(relativePath));
    }

}
