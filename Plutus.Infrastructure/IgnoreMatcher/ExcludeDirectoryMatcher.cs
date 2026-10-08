using MAB.DotIgnore;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.IgnoreMatcher;

internal sealed class ExcludeDirectoryMatcher(IgnoreList ignoreList) : IIgnoreMatcher
{
    public bool IsIgnoredFile(string relativePath) => false;

    public bool IsIgnoredDirectory(string relativePath) => ignoreList.IsIgnored(relativePath, true);
}
