namespace Plutus.Domain;

/// <summary>
/// Defines a service responsible for resolving, expanding, and normalizing file system paths.
/// </summary>
public interface IPathService
{
    /// <summary>
    /// Expands system environment variables and home directory tokens (<c>~</c>)
    /// within the provided path, converting it into a canonical absolute path.
    /// </summary>
    /// <param name="path">The raw file system path string to expand and normalize.</param>
    /// <returns>A fully qualified, deterministic absolute path string.</returns>
    /// <remarks>
    /// Implementations must ensure that cross-platform paths (e.g., using <c>~/</c> or environment variables)
    /// are expanded correctly based on the underlying operating system environment.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the expanded path contains invalid characters or forms an invalid layout.</exception>
    string GetExpandPath(string path);
}
