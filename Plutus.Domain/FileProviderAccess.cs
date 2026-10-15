using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain;

/// <summary>
/// The filesystem as this application uses it: host paths on the outside, an <see cref="IFileProvider"/> on the
/// inside. Every read goes through the provider, so a caller can substitute one that never touches a disk (a test, an
/// embedded host, a virtual tree), while the paths the command line and the messages speak stay the ones a user
/// recognizes.
/// <para>
/// <paramref name="root"/> is the host path the provider's own root corresponds to; a path outside it is simply not
/// found by the provider rather than being resolved by another route.
/// </para>
/// </summary>
public sealed class FileProviderAccess(IFileProvider provider, string root)
{
    /// <summary>The provider that answers for every path this instance is asked about.</summary>
    public IFileProvider Provider { get; } = provider;

    /// <summary>The host path that the provider's root corresponds to.</summary>
    public string Root { get; } = root;

    /// <summary>The provider entry for a host path, which reports whether it exists and what it is.</summary>
    public IFileInfo Info(string hostPath) => Provider.GetFileInfo(ToProviderPath(hostPath));

    /// <summary>True when a file exists at this host path.</summary>
    public bool FileExists(string hostPath) => Info(hostPath) is { Exists: true, IsDirectory: false };

    /// <summary>True when a directory exists at this host path.</summary>
    public bool DirectoryExists(string hostPath) => Info(hostPath) is { Exists: true, IsDirectory: true };

    /// <summary>True when a file or a directory exists at this host path.</summary>
    public bool Exists(string hostPath) => Info(hostPath).Exists;

    /// <summary>
    /// Reads a text file. A missing or unreadable file throws, exactly as reading a file directly does, so a caller
    /// keeps reporting the failure in its own words.
    /// </summary>
    public string ReadAllText(string hostPath, Encoding? encoding = null)
    {
        using var stream = Info(hostPath).CreateReadStream();
        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>The provider path — relative to the provider root, with forward slashes — for a host path.</summary>
    public string ToProviderPath(string hostPath) =>
        Path.GetRelativePath(Root, Path.GetFullPath(hostPath)).Replace('\\', '/');

    /// <summary>The host path a provider path refers to.</summary>
    public string ToHostPath(string providerPath) =>
        Path.GetFullPath(Path.Combine(Root, providerPath.Replace('/', Path.DirectorySeparatorChar)));
}
