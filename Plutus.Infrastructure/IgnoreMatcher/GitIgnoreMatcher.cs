using System.Text;
using MAB.DotIgnore;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.IgnoreMatcher;

public sealed class GitIgnoreMatcher(IgnoreList ignoreList) : IIgnoreMatcher
{
    public bool IsIgnoredFile(string relativePath) => ignoreList.IsIgnored(relativePath, false);

    public bool IsIgnoredDirectory(string relativePath) => ignoreList.IsIgnored(relativePath, true);

    public static GitIgnoreMatcher CreateFromLines(IReadOnlyCollection<string> extraIgnorePatterns)
    {
        var ignoreLines = extraIgnorePatterns.ToHashSet();
        ignoreLines.RemoveWhere(string.IsNullOrWhiteSpace);
        return new GitIgnoreMatcher(new IgnoreList(ignoreLines));
    }

    public static GitIgnoreMatcher CreateFromFolders(List<IFileProvider> roots, List<string> extraIgnorePatterns, Encoding? encoding = null)
    {
        var ignoreLines = roots.SelectMany(e => LoadIgnoreLines(e, encoding)).Concat(extraIgnorePatterns).ToList();
        return CreateFromLines(ignoreLines);
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
