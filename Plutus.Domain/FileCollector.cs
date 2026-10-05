using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class FileCollector(FileService fileService, IIgnoreMatcher ignoreMatcher)
{
    public List<BundleEntry> CollectFiles(DirectoryInfo root, FileInfo output)
    {
        var entries = new List<BundleEntry>();
        var rootFull = root.FullName;
        var outputFull = Path.GetFullPath(output.FullName);

        var stack = new Stack<DirectoryInfo>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            IEnumerable<DirectoryInfo> subDirs;
            IEnumerable<FileInfo> files;

            try
            {
                subDirs = dir.EnumerateDirectories();
                files = dir.EnumerateFiles();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var subDir in subDirs.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (fileService.IsReparsePoint(subDir.Attributes))
                {
                    continue;
                }

                if (fileService.SamePath(subDir.FullName, outputFull))
                {
                    continue;
                }

                var relative = fileService.ToRelative(rootFull, subDir.FullName);
                if (ignoreMatcher.IsIgnoredDirectory(relative))
                {
                    continue;
                }

                stack.Push(subDir);
            }

            foreach (var file in files.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
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
                if (ignoreMatcher.IsIgnoredFile(relative))
                {
                    continue;
                }

                long size;
                try
                {
                    size = file.Length;
                }
                catch
                {
                    continue;
                }

                entries.Add(new BundleEntry(relative, file, size));
            }
        }

        return entries;
    }
}

public sealed record BundleEntry(
    string RelativePath,
    FileInfo File,
    long Size);
