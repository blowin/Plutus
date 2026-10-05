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

    public bool IsIgnoredFile(string relativePath)
    {
        var name = Path.GetFileName(relativePath);

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

        var extension = Path.GetExtension(relativePath);
        return DangerousExtensions.Contains(extension);
    }

    public bool IsIgnoredDirectory(string relativePath) => false;
}
