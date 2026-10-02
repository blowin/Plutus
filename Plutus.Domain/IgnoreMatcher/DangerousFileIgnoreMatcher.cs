namespace Plutus.Domain.IgnoreMatcher;

public class DangerousFileIgnoreMatcher : IIgnoreMatcher
{
    private static readonly HashSet<string> DangerousExactFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".netrc", "_netrc", ".npmrc", ".pypirc",
        "credentials", "credentials.json", "secrets.json", "secret.json",
        "service-account.json", "google-services.json",
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519",
    };

    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pem", ".key", ".p12", ".pfx", ".jks", ".keystore", ".kdbx", ".pwd", ".secret",
    };

    public bool IsIgnoredFile(FileInfo fileInfo)
    {
        var name = fileInfo.Name;

        if (DangerousExactFileNames.Contains(name))
        {
            return true;
        }

        if (name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
        {
            return !(
                name.Equals(".env.example", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(".env.sample", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(".env.template", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(".env.dist", StringComparison.OrdinalIgnoreCase));
        }

        return DangerousExtensions.Contains(fileInfo.Extension);
    }

    public bool IsIgnoredDirectory(DirectoryInfo directoryInfo) => false;

    private static bool SamePath(string left, string right)
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
}
