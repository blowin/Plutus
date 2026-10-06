using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class FileCollector(IFileInfoDetailProvider fileInfoDetailProvider, IIgnoreMatcher ignoreMatcher)
{
    public List<BundleEntry> CollectFiles(IFileProvider fileProvider, string outputPhysicalPath)
    {
        var entries = new List<BundleEntry>();

        var stack = new Stack<(IDirectoryContents Contents, string RelativePath)>();

        var rootContents = fileProvider.GetDirectoryContents("");
        stack.Push((rootContents, ""));

        while (stack.Count > 0)
        {
            var (contents, currentRelPath) = stack.Pop();

            var sortedItems = contents.OrderBy(x => !x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var item in sortedItems)
            {
                if (item.IsSamePath(outputPhysicalPath) || fileInfoDetailProvider.IsReparsePoint(item))
                {
                    continue;
                }

                var itemSubPath = string.IsNullOrEmpty(currentRelPath) ? item.Name : $"{currentRelPath}/{item.Name}";

                if (item.IsDirectory)
                {
                    if (ignoreMatcher.IsIgnoredDirectory(itemSubPath))
                    {
                        continue;
                    }

                    var subContents = fileProvider.GetDirectoryContents(itemSubPath);
                    if (subContents.Exists)
                    {
                        stack.Push((subContents, itemSubPath));
                    }
                }
                else
                {
                    if (ignoreMatcher.IsIgnoredFile(itemSubPath))
                    {
                        continue;
                    }

                    entries.Add(new BundleEntry(itemSubPath, item));
                }
            }
        }

        return entries;
    }
}

public sealed record BundleEntry(string RelativePath, IFileInfo File);
