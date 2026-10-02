using MAB.DotIgnore;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.IgnoreMatcher;

public sealed class GitIgnoreMatcher(IgnoreList ignoreList) : IIgnoreMatcher
{
    public bool IsIgnoredFile(FileInfo fileInfo) => ignoreList.IsIgnored(fileInfo);

    public bool IsIgnoredDirectory(DirectoryInfo directoryInfo) => ignoreList.IsIgnored(directoryInfo);
}
