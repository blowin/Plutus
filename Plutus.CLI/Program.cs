using System.CommandLine;
using System.Text;
using Plutus.Domain.Diagnostics;
using Plutus.Infrastructure.Diagnostics;

namespace Plutus.CLI;

/// <summary>
/// The entry point: one output target for the whole process, one command tree, and nothing else. What the commands do
/// lives in <see cref="BundleCommand"/> and <see cref="InitCommand"/>.
/// </summary>
public static class Program
{
    /// <summary>Tokens that belong to the command tree itself and are never read as a project path.</summary>
    private static readonly IReadOnlySet<string> RootTokens =
        new HashSet<string>(StringComparer.Ordinal) { "--help", "-h", "-?", "/?", "--version" };

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Services report through this one target, so no layer below decides how a run reads and the report of what
        // happened is printed in exactly one place.
        IRunOutput output = new ConsoleRunOutput();

        var rootCommand = new RootCommand("Plutus — Project Bundler. Collects source files into a single document.");
        rootCommand.Subcommands.Add(BundleCommand.Create(output));
        rootCommand.Subcommands.Add(InitCommand.Create(output));

        return await rootCommand.Parse(Normalize(rootCommand, args)).InvokeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Bundling used to be the root command, so <c>plutus . --format md</c> keeps working next to
    /// <c>plutus bundle . --format md</c>: a command line that does not name a command is read as the bundle command.
    /// </summary>
    internal static string[] Normalize(RootCommand root, string[] args)
    {
        if (args.Length == 0 || RootTokens.Contains(args[0]))
        {
            return args;
        }

        var namesACommand = root.Subcommands.Any(command => string.Equals(command.Name, args[0], StringComparison.Ordinal));
        return namesACommand ? args : ["bundle", .. args];
    }
}
