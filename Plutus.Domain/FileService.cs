namespace Plutus.Domain;

public class FileService
{
    public bool IsReparsePoint(FileAttributes attributes) => (attributes & FileAttributes.ReparsePoint) != 0;

    public bool SamePath(string left, string right)
    {
        var leftFull = Path.GetFullPath(left);
        var rightFull = Path.GetFullPath(right);

        return string.Equals(
            leftFull,
            rightFull,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    public string ToRelative(string rootFull, string full) => Path.GetRelativePath(rootFull, full).Replace('\\', '/');
}

