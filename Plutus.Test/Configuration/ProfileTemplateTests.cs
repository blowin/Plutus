using System.Text.Json;
using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Configuration;
using Plutus.Test.Fakes;

namespace Plutus.Test.Configuration;

/// <summary>
/// The generated template is the first thing a user reads, so it is checked against the schema it documents: every
/// key the loader accepts must appear in it, and the file it produces must load as a profile that changes nothing.
/// </summary>
public class ProfileTemplateTests
{
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\plutus-template-tests" : "/plutus-template-tests";

    [Fact]
    public void Template_MentionsEveryKeyTheLoaderAccepts()
    {
        var template = new ProfileTemplate().Render();

        foreach (var property in typeof(PlutusProfile).GetProperties().Where(property => property.CanWrite))
        {
            var key = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            template.Should().Contain($"\"{key}\"", $"the template documents '{key}'");
        }
    }

    [Fact]
    public void Template_StatesWhatAProfileCannotSet()
    {
        var template = new ProfileTemplate().Render();

        template.Should().Contain("--diff-only");
        template.Should().Contain("--branch/--commit");
    }

    [Fact]
    public void Template_LoadsAsAProfileThatChangesNothing()
    {
        // Every key is commented out, so writing the file cannot alter a run: it is valid, and it states nothing.
        var path = Path.Combine(Root, ".plutusrc");
        var loader = new PlutusProfileLoader(new FileProviderAccess(
            new InMemoryFileProvider().AddFile(".plutusrc", new ProfileTemplate().Render()),
            Root));

        var result = loader.Load(path);

        result.Succeeded.Should().BeTrue(result.Error);
        var profile = result.Profile!;
        profile.IncludeExtensions.Should().BeNull();
        profile.ExcludeDirs.Should().BeNull();
        profile.Format.Should().BeNull();
        profile.Minify.Should().BeNull();
    }
}
