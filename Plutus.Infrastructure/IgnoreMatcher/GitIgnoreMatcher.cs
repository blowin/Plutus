using MAB.DotIgnore;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.IgnoreMatcher;

public sealed class GitIgnoreMatcher(IgnoreList ignoreList) : IIgnoreMatcher
{
    public bool IsIgnoredFile(string relativePath) => ignoreList.IsIgnored(relativePath, false);

    public bool IsIgnoredDirectory(string relativePath) => ignoreList.IsIgnored(relativePath, true);
}
