using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class TreeRenderer(IIgnoreMatcher ignoreMatcher, IFileInfoDetailProvider fileInfoDetailProvider)
{
    public List<string> RenderTree(IFileProvider fileProvider, string rootName, string outputPhysicalPath, long maxFileSize)
    {
        var lines = new List<string>();

        void Walk(string subPath, string prefix)
        {
            var contents = fileProvider.GetDirectoryContents(subPath);
            if (!contents.Exists)
            {
                return;
            }

            var children = contents.OrderBy(x => !x.IsDirectory)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (!string.IsNullOrEmpty(outputPhysicalPath) &&
                    !string.IsNullOrEmpty(child.PhysicalPath) &&
                    string.Equals(Path.GetFullPath(child.PhysicalPath), Path.GetFullPath(outputPhysicalPath), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fileInfoDetailProvider.IsReparsePoint(child))
                {
                    continue;
                }

                var isLast = i == children.Count - 1;
                var branch = isLast ? "└── " : "├── ";
                var nextPrefix = prefix + (isLast ? "    " : "│   ");

                var itemSubPath = string.IsNullOrEmpty(subPath) ? child.Name : $"{subPath}/{child.Name}";
                if (child.IsDirectory)
                {
                    var marker = DirectoryMarker(itemSubPath);
                    var postfix = string.IsNullOrEmpty(marker) ? "/" : marker;
                    lines.Add($"{prefix}{branch}{child.Name}{postfix}");

                    if (marker.Length == 0)
                    {
                        Walk(itemSubPath, nextPrefix);
                    }
                }
                else
                {
                    var marker = FileMarker(itemSubPath, child, ignoreMatcher, maxFileSize);

                    lines.Add($"{prefix}{branch}{child.Name}{marker}");
                }
            }
        }

        lines.Add($"{rootName}/");
        Walk("", "");

        return lines;
    }

    private string DirectoryMarker(string relative) => ignoreMatcher.IsIgnoredDirectory(relative) ? " [IGNORED DIR]" : string.Empty;

    private static string FileMarker(string relative, IFileInfo file, IIgnoreMatcher ignoreMatcher, long maxFileSize)
    {
        if (ignoreMatcher.IsIgnoredFile(relative))
        {
            return " [IGNORED FILE]";
        }

        if (file.Length > maxFileSize)
        {
            return $" [LARGE {file.Length.HumanSize()}]";
        }

        return $" [{file.Length.HumanSize()}]";
    }
}

