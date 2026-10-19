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

        var analyzer = CreateAnalyzer();

        var result = await analyzer.AnalyzeAsync("src/Program.cs", provider, CancellationToken.None);

        result.Dependencies.Should().HaveCount(2);
        result.Dependencies.Should().Contain(d => d.ResolvedVirtualPath == "src/Core.cs" && d.Reason.Contains("using"));
        result.Dependencies.Should().Contain(d => d.ResolvedVirtualPath == "src/Utils.cs" && d.Reason.Contains("namespace"));
    }

    [Fact]
    public async Task AnalyzeAsync_SupportsImplicitGlobalUsingsFromIndex()
    {
        // Arrange
        // Файл Usings.cs объявляет глобальный импорт для MyApp.Core.
        // За счет этого src/Utils.cs должен неявно связаться с src/Core.cs,
        // хотя в самом Utils.cs нет локальных директив using.
        var provider = new InMemoryFileProvider()
            .AddFile("src/Usings.cs", """
                                      global using MyApp.Core;
                                      """)
            .AddFile("src/Core.cs", """
                                    namespace MyApp.Core;
                                    public class CoreData {}
                                    """)
            .AddFile("src/Utils.cs", """
                                     namespace MyApp.Utils;
                                     public class Logger {}
                                     """);

        var analyzer = CreateAnalyzer();

        // Act
        var result = await analyzer.AnalyzeAsync("src/Utils.cs", provider, CancellationToken.None);

        // Assert
        // Проверяем, что Utils.cs успешно обнаружил неявную зависимость от Core.cs через общую карту индекса
        result.Dependencies.Should().Contain(d => d.ResolvedVirtualPath == "src/Core.cs");
    }

    [Fact]
    public async Task AnalyzeAsync_HandlesUsingStaticAndNamespaceAliasesSafely()
    {
        // Arrange
        var provider = new InMemoryFileProvider()
            .AddFile("src/Extensions.cs", """
                                    namespace MyApp.Extensions;
                                    public static class StringExtensions { public static void Log() {} }
                                    """)
            .AddFile("src/Component.cs", """
                                    namespace MyApp.Components;
                                    public class UIComponent {}
                                    """)
            .AddFile("src/Program.cs", """
                                       using static MyApp.Extensions.StringExtensions;
                                       using Comp = MyApp.Components;

                                       namespace MyApp;
                                       public class Program {}
                                       """);

        var analyzer = CreateAnalyzer();

        // Act
        var result = await analyzer.AnalyzeAsync("src/Program.cs", provider, CancellationToken.None);

        // Assert
        // Провайдер должен успешно нормализовать 'using static' до пространства импорта 'MyApp.Extensions'
        result.Dependencies.Should().Contain(d => d.ResolvedVirtualPath == "src/Extensions.cs");

        // Провайдер должен успешно нормализовать alias-объявление до 'MyApp.Components'
        result.Dependencies.Should().Contain(d => d.ResolvedVirtualPath == "src/Component.cs");
    }

    private static CSharpSourceAnalyzer CreateAnalyzer()
    {
        var normalizer = new UsingNormalizer();
        var roslynTreeCache = new RoslynTreeCache(new CacheReadProvider(Encoding.UTF8));
        var index = new CSharpNamespaceIndex(roslynTreeCache, normalizer);
        return new CSharpSourceAnalyzer(index, roslynTreeCache, normalizer);
    }
}
