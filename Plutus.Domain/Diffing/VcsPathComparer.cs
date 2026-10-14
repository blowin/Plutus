namespace Plutus.Domain.Diffing;

/// <summary>
/// Single source of truth for how diff paths are compared. Git stores paths case-sensitively, but Windows
/// and macOS filesystems do not, and the isolated subset must match the file provider used for scanning.
/// </summary>
public static class VcsPathComparer
{
    public static bool IgnoreCase { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public static StringComparer Comparer { get; } = IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
