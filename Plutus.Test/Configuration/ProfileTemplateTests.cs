using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Configuration;

namespace Plutus.Test.Configuration;

/// <summary>
/// The generated profile is the first thing a user edits, so it is checked against the schema it writes: every key
/// the loader accepts is present with a value, the loader reads it back unchanged, and the values are the ones a run
/// would have used anyway.
/// </summary>
public class ProfileTemplateTests
{
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\plutus-template-tests" : "/plutus-template-tests";

    [Fact]
    public void Template_ContainsEveryKeyTheLoaderAccepts()
    {
        var template = new ProfileTemplate().Render();

        foreach (var property in typeof(PlutusProfile).GetProperties().Where(property => property.CanWrite))
        {
            var key = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            template.Should().Contain($"\"{key}\"", $"the template writes '{key}'");
        }
    }

    [Fact]
    public void Template_ComesBackUnchangedThroughTheLoader()
    {
        var loader = new PlutusProfileLoader(new FileProviderAccess(
            new Fakes.InMemoryFileProvider().AddFile(".plutusrc", new ProfileTemplate().Render()),
            Root));

        var result = loader.Load(Path.Combine(Root, ".plutusrc"));

        result.Succeeded.Should().BeTrue(result.Error);
        result.Profile.Should().BeEquivalentTo(ProfileTemplate.Starter);
    }

    [Fact]
    public void Template_StatesWhatARunWouldUseAnyway()
    {
        // The starter values are the defaults, so generating a profile cannot change what a run reads. The one thing
        // it states outright is the junk directories, which the built-in exclusions already skip.
        var starter = new RunOptionsResolver().Resolve(new RunOptionsRequest { ProjectPath = ["."] }, ProfileTemplate.Starter, Path.Combine(Root, ".plutusrc")).Options!;
        var withoutProfile = new RunOptionsResolver().Resolve(new RunOptionsRequest { ProjectPath = ["."] }).Options!;

        starter.ExcludeDirPatterns.Should().Equal(ProfileTemplate.Starter.ExcludeDirs);
        starter.Profile!.AppliedKeys.Should().Equal(
            new[] { "excludeDirs" },
            "it is the only key that states something the default does not");
        starter.Profile.DefaultedKeys.Should().BeEquivalentTo(
            [
                "maxFileSize","useGitignore","useDefaultExcludes","allowDangerousFiles","minify","stripComments",
                "preserveDocComments","commentStrategy","signaturesOnly","encoding","maxConcurrency","format",
                "includeFileContents","maxDownloadSize","maxExtractedSize","maxFileCount","maxDepth",
                "rejectDuplicatePaths","rejectSymlinks"
            ],
            "every other key restates the value a run would have used; the token policy keys are only read when a budget is set");
        starter.Format.Should().Be(withoutProfile.Format);
        starter.MaxConcurrency.Should().Be(withoutProfile.MaxConcurrency);
        starter.MaxFileSize.Should().Be(withoutProfile.MaxFileSize);
        starter.TargetEncoding.WebName.Should().Be(withoutProfile.TargetEncoding.WebName);
        starter.UseGitignore.Should().Be(withoutProfile.UseGitignore);
        starter.UseDefaultExcludes.Should().Be(withoutProfile.UseDefaultExcludes);
        starter.AllowDangerous.Should().Be(withoutProfile.AllowDangerous);
        starter.IncludeFileContents.Should().Be(withoutProfile.IncludeFileContents);
        starter.Minify.Should().Be(withoutProfile.Minify);
        starter.StripComments.Should().Be(withoutProfile.StripComments);
        starter.PreserveDocumentationComments.Should().Be(withoutProfile.PreserveDocumentationComments);
        starter.CommentStrategy.Should().Be(withoutProfile.CommentStrategy);
        starter.SignaturesOnly.Should().Be(withoutProfile.SignaturesOnly);
        starter.TokenBudget.Should().BeNull();
        starter.IncludeFilePatterns.Should().BeEmpty();
        starter.ExcludeFilePatterns.Should().BeEmpty();
        starter.IncludeDirPatterns.Should().BeEmpty();
        starter.ExcludePatterns.Should().BeEmpty();
        starter.OutputPath.Should().BeNull();
    }

    [Fact]
    public void Template_IsPlainJsonWithoutCommentary()
    {
        // A profile that has to be uncommented before it does anything is not a starting point; the values are there
        // to be edited, so the file carries no comments and no '//' prefixes.
        var template = new ProfileTemplate().Render();

        template.Should().NotContain("//");
        template.Should().StartWith("{");
        template.TrimEnd().Should().EndWith("}");
    }
}
