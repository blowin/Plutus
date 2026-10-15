using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.Infrastructure;

public sealed class AdditionalIgnoreMatcherProvider(IRunOutput? runOutput = null) : IAdditionalIgnoreMatcherProvider
{
    private readonly IRunOutput _output = runOutput ?? NullRunOutput.Instance;

    public IEnumerable<IIgnoreMatcher> GetMatchers(List<IFileProvider> roots, RunAppOptions options)
    {
        if (options.UseGitignore)
        {
            yield return GitIgnoreMatcher.FromFolders(roots, options.ExcludePatterns, runOutput: _output);
        }
        else
        {
            yield return GitIgnoreMatcher.FromLines(options.ExcludePatterns, _output);
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
