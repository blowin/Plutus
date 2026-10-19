using System.Text;
using FluentAssertions;
using Plutus.Domain.BundleWriters.CacheReadProviders;
using Plutus.Infrastructure.Dependencies.Csharp;
using Plutus.Test.Fakes;

namespace Plutus.Test.Dependencies;

public class CSharpSourceAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_ResolvesUsingDirectivesAndSharedNamespaces()
    {
        var provider = new InMemoryFileProvider()
            .AddFile("src/Core.cs", """
                                    namespace MyApp.Core;
                                    public class CoreService {}
                                    """)
            .AddFile("src/Utils.cs", """
                                     namespace MyApp;
                                     public class Helper {}
                                     """)
            .AddFile("src/Program.cs", """
                                       using MyApp.Core;
                                       namespace MyApp;
                                       public class Program {}
                                       """);

        var roslynTreeCache = new RoslynTreeCache(new CacheReadProvider(Encoding.UTF8));
        var index = new CSharpNamespaceIndex(roslynTreeCache);
        var analyzer = new CSharpSourceAnalyzer(index, roslynTreeCache);

        var result = await analyzer.AnalyzeAsync("src/Program.cs", provider, CancellationToken.None);

        result.Dependencies.Should().HaveCount(2);
        result.Dependencies.Should().Contain(d => d.ResolvedVirtualPath == "src/Core.cs" && d.Reason.Contains("using"));
        result.Dependencies.Should().Contain(d => d.ResolvedVirtualPath == "src/Utils.cs" && d.Reason.Contains("namespace"));
    }
}
