using System.Text;
using MAB.DotIgnore;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.IgnoreMatcher;

public sealed class GitIgnoreMatcher(IgnoreList ignoreList) : IIgnoreMatcher
{
    public bool IsIgnoredFile(string relativePath) => ignoreList.IsIgnored(relativePath, false);

    public bool IsIgnoredDirectory(string relativePath) => ignoreList.IsIgnored(relativePath, true);

    public static IIgnoreMatcher FromIncludeFiles(IReadOnlyCollection<string> extraIgnorePatterns)
        => CreateIgnoreMatcherOrEmpty(extraIgnorePatterns, e => new IncludeFileMatcher(e));

    public static IIgnoreMatcher FromIncludeDirs(IReadOnlyCollection<string> extraIgnorePatterns)
        => CreateIgnoreMatcherOrEmpty(extraIgnorePatterns, e => new IncludeDirectoryMatcher(e));

    public static IIgnoreMatcher FromExcludeFiles(IReadOnlyCollection<string> extraIgnorePatterns)
        => CreateIgnoreMatcherOrEmpty(extraIgnorePatterns, e => new ExcludeFileMatcher(e));

    public static IIgnoreMatcher FromExcludeDirs(IReadOnlyCollection<string> extraIgnorePatterns)
        => CreateIgnoreMatcherOrEmpty(extraIgnorePatterns, e => new ExcludeDirectoryMatcher(e));

    public static IIgnoreMatcher FromLines(IReadOnlyCollection<string> extraIgnorePatterns)
        => CreateIgnoreMatcherOrEmpty(extraIgnorePatterns, e => new GitIgnoreMatcher(e));

    public static IIgnoreMatcher FromFolders(List<IFileProvider> roots, List<string> ignorePatterns, Encoding? encoding = null)
    {
        var ignoreLines = roots.SelectMany(e => LoadIgnoreLines(e, encoding)).Concat(ignorePatterns).ToList();
        return FromLines(ignoreLines);
    }

    private static IIgnoreMatcher CreateIgnoreMatcherOrEmpty(IEnumerable<string> ignorePatterns, Func<IgnoreList, IIgnoreMatcher> factory)
    {
        var ignoreLines = ignorePatterns.ToHashSet();
        ignoreLines.RemoveWhere(string.IsNullOrWhiteSpace);
        return ignoreLines.Count == 0 ? EmptyIgnoreMatcher.Instance : factory(new IgnoreList(ignoreLines));
    }

    private static List<string> LoadIgnoreLines(IFileProvider provider, Encoding? encoding = null)
    {
        var lines = new List<string>();
        var gitIgnoreFile = provider.GetFileInfo(".gitignore");
        if (!gitIgnoreFile.Exists)
        {
            return lines;
        }

        try
        {
            using var stream = gitIgnoreFile.CreateReadStream();
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: failed to read {gitIgnoreFile.PhysicalPath ?? gitIgnoreFile.Name}: {ex.Message}");
        }

        return lines;
    }
}
