namespace Plutus.Domain.IgnoreMatcher;

public class JunkDirectoryIgnoreMatcher : IIgnoreMatcher
{
    private static readonly HashSet<string> DefaultExcludeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".bmp", ".tiff", ".svg",
        ".mp3", ".mp4", ".mov", ".avi", ".mkv", ".wav", ".flac",
        ".zip", ".tar", ".gz", ".bz2", ".xz", ".7z", ".rar",
        ".exe", ".dll", ".so", ".dylib", ".bin", ".obj", ".o", ".a", ".lib",
        ".pyc", ".pyo", ".pyd", ".class", ".jar", ".war", ".ear",
        ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb", ".wal", ".shm",
        ".log", ".tmp", ".temp", ".bak", ".swp", ".swo", ".lock", ".pid",
        ".pem", ".key", ".p12", ".pfx", ".jks", ".keystore", ".kdbx",
    };

    private static readonly HashSet<string> DefaultExcludeDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".hg", ".svn",
        ".idea", ".vs", ".vscode", ".fleet",
        "__pycache__", ".pytest_cache", ".mypy_cache", ".ruff_cache", ".tox", ".nox",
        "node_modules", "bower_components", "jspm_packages",
        ".next", ".nuxt", ".svelte-kit", ".astro", ".turbo",
        "venv", ".venv", "env", ".env", "virtualenv",
        "dist", "build", "out", "target", "bin", "obj",
        "coverage", ".coverage", "htmlcov", ".nyc_output",
        ".eggs", ".cache", ".parcel-cache",
        ".terraform", ".serverless",
        "packages",
    };

    public bool IsIgnoredFile(string relativePath) => DefaultExcludeExtensions.Contains(Path.GetExtension(relativePath));

    public bool IsIgnoredDirectory(string relativePath)
    {
        var cleanedPath = relativePath.TrimEnd('/', '\\');
        var lastSlashIndex = cleanedPath.LastIndexOfAny(['/', '\\']);
        var name = lastSlashIndex >= 0
            ? cleanedPath[(lastSlashIndex + 1)..]
            : Path.GetDirectoryName(cleanedPath);
        if (string.IsNullOrEmpty(name))
        {
            name = relativePath;
        }

        return DefaultExcludeDirs.Contains(name) ||
               name.EndsWith(".egg-info", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith(".dsh", StringComparison.OrdinalIgnoreCase);
    }
}

