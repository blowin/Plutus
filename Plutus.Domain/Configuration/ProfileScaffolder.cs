using System.Text;

namespace Plutus.Domain.Configuration;

/// <summary>The outcome of writing a profile template: the path, or the reason it was not written.</summary>
public sealed record ProfileScaffoldResult
{
    public string? Path { get; init; }

    public string? Error { get; init; }

    public bool Written => Path is not null && Error is null;
}

/// <summary>
/// Writes a starter profile. It refuses to overwrite an existing file unless it is told to, because the profile of a
/// repository is a checked-in file that a user has already filled in, and the file access is the same abstraction the
/// rest of the application uses, so this works wherever a run does.
/// </summary>
public sealed class ProfileScaffolder(
    IFileInfoDetailProvider detailProvider,
    FileProviderAccess files,
    ProfileTemplate template)
{
    /// <summary>The profile name a run looks for first.</summary>
    public const string DefaultFileName = ".plutusrc";

    /// <summary>Writes the template to <paramref name="path"/>, or reports why it did not.</summary>
    public ProfileScaffoldResult Scaffold(string path, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ProfileScaffoldResult { Error = "no profile path was given" };
        }

        if (!overwrite && files.FileExists(path))
        {
            return new ProfileScaffoldResult { Error = $"'{path}' already exists; pass --force to replace it" };
        }

        try
        {
            var file = detailProvider.CreateFile(path);
            detailProvider.CreateDirectoryForFile(file);

            // A configuration file is read by people and by other tools: no byte-order mark, UTF-8 text.
            using var writer = detailProvider.CreateWriterForFile(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(template.Render());
        }
        catch (Exception exception)
        {
            return new ProfileScaffoldResult { Error = $"cannot write '{path}': {exception.Message}" };
        }

        return new ProfileScaffoldResult { Path = path };
    }
}
