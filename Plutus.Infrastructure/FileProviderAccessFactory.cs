using Plutus.Domain;

namespace Plutus.Infrastructure;

/// <summary>
/// Builds the file access a run reads its configuration through. The provider is rooted at the volume that holds the
/// working directory, which is what lets a profile be searched from there up to the repository root; the access
/// translates between host paths and provider paths so nothing above this class has to know the difference.
/// </summary>
public sealed class FileProviderAccessFactory(IFileInfoDetailProvider detailProvider)
{
    /// <summary>The access rooted where a run starts: the volume of the current working directory.</summary>
    public FileProviderAccess CreateForWorkingDirectory() => CreateFor(detailProvider.GetCurrentDirectory());

    /// <summary>The access rooted at the volume of <paramref name="workingDirectory"/>.</summary>
    public FileProviderAccess CreateFor(string workingDirectory)
    {
        var root = Path.GetPathRoot(workingDirectory);
        if (string.IsNullOrEmpty(root))
        {
            // A path with no volume (a relative path, or a platform that has none): the working directory is as far
            // up as the search can go.
            root = workingDirectory;
        }

        return new FileProviderAccess(detailProvider.CreateFileProvider(detailProvider.CreateDirectory(root)), root);
    }
}
