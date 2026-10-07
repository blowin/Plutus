namespace Plutus.Infrastructure;

public static class StringExt
{
    extension(string self)
    {
        public string GetExpandPath()
        {
            var path = Environment.ExpandEnvironmentVariables(self);

            if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var rest = path.TrimStart('~').TrimStart('/', '\\');
                path = string.IsNullOrEmpty(rest) ? home : Path.Combine(home, rest);
            }

            return Path.GetFullPath(path);
        }
    }
}
