using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain;

public static class FileInfoExt
{
    public static bool IsSamePath(this IFileInfo self, string path) =>
        !string.IsNullOrEmpty(path) &&
        !string.IsNullOrEmpty(self.PhysicalPath) &&
        string.Equals(Path.GetFullPath(self.PhysicalPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
}
