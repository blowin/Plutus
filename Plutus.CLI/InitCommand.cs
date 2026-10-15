using System.CommandLine;
using Plutus.Domain;
using Plutus.Domain.Configuration;
using Plutus.Domain.Diagnostics;
using Plutus.Infrastructure;

namespace Plutus.CLI;

/// <summary>
/// The <c>init</c> command: writes a commented <c>.plutusrc</c> to start from, so nobody has to learn the schema by
/// reading the documentation and typing it out.
/// </summary>
internal static class InitCommand
{
    public static Command Create(IRunOutput output)
    {
        var pathOption = new Option<string?>("--output")
        {
            Description = $"Where to write the profile. Default: {ProfileScaffolder.DefaultFileName} in the current directory."
        };
        pathOption.Aliases.Add("-o");

        var forceOption = new Option<bool>("--force")
        {
            Description = "Replace an existing profile instead of refusing to touch it."
        };

        var command = new Command("init", "Write a commented .plutusrc profile template to start from. Takes no project path.")
        {
            pathOption,
            forceOption,
        };

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathOption);
            var detailProvider = new PhysicianPlutusFileInfo();
            var files = new FileProviderAccessFactory(detailProvider).CreateForWorkingDirectory();
            var scaffolder = new ProfileScaffolder(detailProvider, files, new ProfileTemplate());
            var target = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(detailProvider.GetCurrentDirectory(), ProfileScaffolder.DefaultFileName)
                : path;

            var result = scaffolder.Scaffold(target, parseResult.GetValue(forceOption));
            if (!result.Written)
            {
                output.Error(result.Error!);
                return 1;
            }

            output.Info($"Profile template written: {result.Path}");
            output.Info("Every key is commented out, so the file changes nothing until you uncomment what you need.");
            output.Info("A run from this directory or below picks it up automatically; --no-profile ignores it.");
            return 0;
        });

        return command;
    }
}
