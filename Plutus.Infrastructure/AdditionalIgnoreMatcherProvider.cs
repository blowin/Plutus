using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.Infrastructure;

public sealed class AdditionalIgnoreMatcherProvider : IAdditionalIgnoreMatcherProvider
{
    public IEnumerable<IIgnoreMatcher> GetMatchers(List<IFileProvider> roots, RunAppOptions options)
    {
        if (options.UseGitignore)
        {
            yield return GitIgnoreMatcher.FromFolders(roots, options.ExcludePatterns);
        }
        else
        {
            yield return GitIgnoreMatcher.FromLines(options.ExcludePatterns);
        }

        if (options.ExcludeFilePatterns.Count > 0)
        {
            yield return GitIgnoreMatcher.FromExcludeFiles(options.ExcludeFilePatterns);
        }

        if (options.ExcludeDirPatterns.Count > 0)
        {
            yield return GitIgnoreMatcher.FromExcludeDirs(options.ExcludeDirPatterns);
        }

        if (options.IncludeFilePatterns.Count > 0)
        {
            yield return GitIgnoreMatcher.FromIncludeFiles(options.IncludeFilePatterns);
        }

        if (options.IncludeDirPatterns.Count > 0)
        {
            yield return GitIgnoreMatcher.FromIncludeDirs(options.IncludeDirPatterns);
        }
    }
}
