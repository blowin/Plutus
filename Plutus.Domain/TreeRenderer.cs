using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class TreeRenderer(FileService fileService, IIgnoreMatcher ignoreMatcher)
{
    public List<string> RenderTree(DirectoryInfo root, FileInfo output, long maxFileSize)
    {
        var lines = new List<string>();
        var rootFull = root.FullName;
        var outputFull = Path.GetFullPath(output.FullName);

        void Walk(DirectoryInfo directory, string prefix)
        {
            var children = new List<FileSystemInfo>();

            try
            {
                children.AddRange(directory.EnumerateDirectories());
                children.AddRange(directory.EnumerateFiles());
            }
            catch (UnauthorizedAccessException)
            {
                lines.Add($"{prefix}[unreadable directory: {directory.Name}]");
                return;
            }
            catch (IOException ex)
            {
                lines.Add($"{prefix}[unreadable directory: {directory.Name}: {ex.Message}]");
                return;
            }

            children.Sort((a, b) =>
            {
                var aIsFile = a is FileInfo ? 1 : 0;
                var bIsFile = b is FileInfo ? 1 : 0;
                var byType = aIsFile.CompareTo(bIsFile);
                if (byType != 0)
                {
                    return byType;
                }

                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var isLast = i == children.Count - 1;
                var branch = isLast ? "└── " : "├── ";
                var nextPrefix = prefix + (isLast ? "    " : "│   ");

                if (child is DirectoryInfo dir)
                {
                    if (fileService.IsReparsePoint(dir.Attributes))
                    {
                        continue;
                    }

                    if (fileService.SamePath(dir.FullName, outputFull))
                    {
                        continue;
                    }

                    var relative = fileService.ToRelative(rootFull, dir.FullName);
                    var marker = DirectoryMarker(relative);

                    lines.Add($"{prefix}{branch}{dir.Name}/{marker}");

                    if (marker.Length == 0)
                    {
                        Walk(dir, nextPrefix);
                    }
                }
                else if (child is FileInfo file)
                {
                    if (fileService.IsReparsePoint(file.Attributes))
                    {
                        continue;
                    }

                    if (fileService.SamePath(file.FullName, outputFull))
                    {
                        continue;
                    }

                    var relative = fileService.ToRelative(rootFull, file.FullName);
                    var marker = FileMarker(relative, file, ignoreMatcher, maxFileSize);

                    lines.Add($"{prefix}{branch}{file.Name}{marker}");
                }
            }
        }

        lines.Add($"{root.Name}/");
        Walk(root, "");

        return lines;
    }

    private string DirectoryMarker(string relative) => ignoreMatcher.IsIgnoredDirectory(relative) ? " [IGNORED DIR]" : string.Empty;

    private static string FileMarker(string relative, FileInfo file, IIgnoreMatcher ignoreMatcher, long maxFileSize)
    {
        if (ignoreMatcher.IsIgnoredFile(relative))
        {
            return " [IGNORED FILE]";
        }

        long size;
        try
        {
            size = file.Length;
        }
        catch
        {
            return " [UNREADABLE]";
        }

        if (size > maxFileSize)
        {
            return $" [LARGE {size.HumanSize()}]";
        }

        return $" [{size.HumanSize()}]";
    }
}

