using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Domain.Diagnostics;
using Plutus.Infrastructure;
using Plutus.Infrastructure.Diagnostics;

namespace Plutus.CLI;

/// <summary>
/// Runs one bundle: builds the container for the resolved options, runs it, and prints the report. The command object
/// decides what a run means; this decides how it is hosted, so both stay small and neither knows the other's details.
/// </summary>
internal static class BundleRunHost
{
    public static async Task<int> RunAsync(RunAppOptions options, IRunOutput output, FileProviderAccess files)
    {
        Console.OutputEncoding = options.TargetEncoding;
        Console.InputEncoding = options.TargetEncoding;

        var services = new ServiceCollection();
        services.AddInfrastructureServices(options, output, files);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
        await using var scope = provider.CreateAsyncScope();

        var exitCode = await scope.ServiceProvider.GetRequiredService<BundleAppRunner>().RunAsync(options);

        scope.ServiceProvider.GetRequiredService<IRunReportWriter>().Write();

        return exitCode;
    }
}
