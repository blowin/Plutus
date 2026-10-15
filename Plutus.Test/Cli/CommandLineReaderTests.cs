using System.CommandLine;
using FluentAssertions;
using Plutus.CLI;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Diagnostics;
using Plutus.Test.Fakes;

namespace Plutus.Test.Cli;

/// <summary>
/// The command line is read as "stated or not stated", because that is what decides whether a profile may fill a
/// value in. These tests pin that distinction, and the command surface a user sees.
/// </summary>
public class CommandLineReaderTests
{
    [Fact]
    public void Explicit_IsNullForAnOptionTheUserDidNotType_EvenWhenItHasADefault()
    {
        var (reader, options) = Parse(".");

        reader.Explicit(options.Format).Should().BeNull();
        reader.ExplicitValue(options.MaxConcurrency).Should().BeNull();
        reader.ExplicitValue(options.UseDefaultExcludes).Should().BeNull();
        reader.ExplicitValue(options.CommentStrategy).Should().BeNull();
    }

    [Fact]
    public void Explicit_ReturnsWhatTheUserTyped()
    {
        var (reader, options) = Parse(".", "--format", "json", "--max-concurrency", "2", "--comment-strategy", "lexical");

        reader.Explicit(options.Format).Should().Be("json");
        reader.ExplicitValue(options.MaxConcurrency).Should().Be(2);
        reader.ExplicitValue(options.CommentStrategy).Should().Be(CommentStrategyRequest.Lexical);
    }

    [Fact]
    public void ExplicitValue_ReportsAFlagThatWasTurnedOffExplicitly()
    {
        var (reader, options) = Parse(".", "--minify", "false");

        reader.ExplicitValue(options.Minify).Should().BeFalse("a typed flag overrides a profile, including with 'false'");
    }

    [Fact]
    public void ExplicitValue_ReportsAFlagAsTyped()
    {
        var (typed, options) = Parse(".", "--minify");
        var (omitted, _) = Parse(".");

        typed.ExplicitValue(options.Minify).Should().BeTrue();
        omitted.ExplicitValue(options.Minify).Should().BeNull();
    }

    [Fact]
    public void ExplicitInverse_ReadsTheNegatedFlag()
    {
        var (typed, options) = Parse(".", "--no-gitignore");
        var (omitted, _) = Parse(".");

        typed.ExplicitInverse(options.NoGitignore).Should().BeFalse("--no-gitignore means 'do not read .gitignore'");
        omitted.ExplicitInverse(options.NoGitignore).Should().BeNull();
    }

    [Fact]
    public void Value_ReportsTheDefaultForAnOptionThatWasNotTyped()
    {
        var (reader, options) = Parse(".");

        reader.Value(options.Format).Should().Be("md");
        reader.Value(options.MaxConcurrency).Should().Be(8);
        reader.Value(options.MaxTokens).Should().BeNull();
    }

    [Fact]
    public void Argument_ReportsTheProjectPaths()
    {
        var (reader, options) = Parse("one", "two");

        reader.Argument(options.ProjectPath).Should().Equal("one", "two");
    }

    [Fact]
    public void CommandSurface_OffersBundlingAndInit()
    {
        var command = BundleCommand.Create(new RecordingRunOutput());

        command.Name.Should().NotBeNullOrEmpty();
        command.Subcommands.Should().ContainSingle().Which.Name.Should().Be("init");
        command.Options.Should().Contain(option => option.Name == "--profile");
        command.Options.Should().Contain(option => option.Name == "--no-profile");
        command.Subcommands.Single().Options.Select(option => option.Name).Should().Contain(["--output", "--force"]);
    }

    private static (CommandLineReader Reader, BundleOptions Options) Parse(params string[] args)
    {
        var options = new BundleOptions().WithAliases();
        var command = new RootCommand();
        command.Add(options.ProjectPath);
        foreach (var option in options.Options())
        {
            command.Add(option);
        }

        return (new CommandLineReader(command.Parse(args)), options);
    }
}
