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
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Services report through this one target, so no layer below decides how a run reads and the report of what
        // happened is printed in exactly one place.
        IRunOutput output = new ConsoleRunOutput();

        var rootCommand = BundleCommand.Create(output);
        return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
    }
}
