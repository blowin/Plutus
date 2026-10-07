using Plutus.Domain;

namespace Plutus.Infrastructure;

public sealed class FileSystemPathService : IPathService
{
    public string GetExpandPath(string path)
    {
        var eePath = Environment.ExpandEnvironmentVariables(path);

        if (eePath == "~" || eePath.StartsWith("~/", StringComparison.Ordinal) || eePath.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var rest = eePath.TrimStart('~').TrimStart('/', '\\');
            eePath = string.IsNullOrEmpty(rest) ? home : Path.Combine(home, rest);
        }

        return Path.GetFullPath(eePath);
    }
}
