using MAB.DotIgnore;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.IgnoreMatcher;

internal sealed class IncludeFileMatcher(IgnoreList ignoreList) : IIgnoreMatcher
{
    public bool IsIgnoredFile(string relativePath) => !ignoreList.IsIgnored(relativePath, false);

    public bool IsIgnoredDirectory(string relativePath) => false;
}
