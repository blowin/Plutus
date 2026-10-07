using FluentAssertions;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class DirectoryExcludeMatcherTests
{
    // Инициализируем матчер масками: точное имя, маска с *, маска с ?, имя с точкой
    private readonly DirectoryExcludeMatcher _matcher = new(new[] 
    { 
        "frontend", 
        "build_*", 
        "*test?", 
        "exact.dir" 
    });

    [Theory]
    [InlineData("frontend", true)]             // Точное совпадение
    [InlineData("src/frontend", true)]         // Вложенность
    [InlineData("src/sub/frontend", true)]     // Глубокая вложенность
    [InlineData("build_release", true)]        // Маска build_*
    [InlineData("project/build_debug", true)]  // Маска build_* во вложенности
    [InlineData("my_test1", true)]             // Маска *test? (test + 1 любой символ)
    [InlineData("a/b/c/unittest2", true)]      // Маска *test?
    [InlineData("exact.dir", true)]            // Точка в имени (не должна ломать regex)
    [InlineData("FRONTEND", true)]             // Регистронезависимость
    [InlineData("backend", false)]             // Не подходит ни под одну маску
    [InlineData("src/backend", false)]         
    [InlineData("build", false)]               // Не подходит под build_* (нет продолжения)
    [InlineData("test", false)]                // Не подходит под *test? (? требует ровно 1 символ)
    public void IsIgnoredDirectory_ShouldHandleMasksAndExactNames(string path, bool expected)
    {
        // Act
        var result = _matcher.IsIgnoredDirectory(path);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsIgnoredFile_ShouldAlwaysReturnFalse()
    {
        // Файлы не должны игнорироваться этим матчером, даже если их имя похоже на папку
        _matcher.IsIgnoredFile("frontend").Should().BeFalse();
        _matcher.IsIgnoredFile("src/build_release/file.txt").Should().BeFalse();
    }
    
    [Fact]
    public void IsIgnoredDirectory_ShouldBeCaseInsensitive()
    {
        var matcher = new DirectoryExcludeMatcher(new[] { "FrontEnd" });
        
        matcher.IsIgnoredDirectory("src/frontend").Should().BeTrue();
        matcher.IsIgnoredDirectory("src/FRONTEND").Should().BeTrue();
        matcher.IsIgnoredDirectory("src/FrOnTeNd").Should().BeTrue();
    }
}
