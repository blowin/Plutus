using MAB.DotIgnore;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.IgnoreMatcher;

public sealed class GitIgnoreMatcher(IgnoreList ignoreList) : IIgnoreMatcher
{
    public bool IsIgnoredFile(string relativePath)
    {
        var r = ignoreList.IsIgnored(relativePath, false);
        return r;
    }

    public bool IsIgnoredDirectory(string relativePath)
    {
        var r = ignoreList.IsIgnored(relativePath, true);
        return r;
    }
}
