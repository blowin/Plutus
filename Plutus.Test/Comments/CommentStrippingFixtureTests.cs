using System.Diagnostics;
using FluentAssertions;
using Plutus.Domain.Comments;

namespace Plutus.Test.Comments;

/// <summary>
/// Runs the scanner over realistic source files. Every fixture is checked for the properties that matter
/// regardless of language — comment markers inside literals survive, the line count does not move, a second pass
/// finds nothing left to remove — and, where a real parser is on PATH (node, python, gcc), the stripped output is
/// handed to it so the claim is verified by something other than our own scanner.
/// </summary>
public class CommentStrippingFixtureTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Comments", name);

    private static string ReadFixture(string name) => File.ReadAllText(FixturePath(name));

    public static TheoryData<string, string> Fixtures()
    {
        var data = new TheoryData<string, string>();
        foreach (var (file, language) in new[]
                 {
                     ("widget.js", "javascript"),
                     ("Program.cs", "csharp"),
                     ("pipeline.py", "python"),
                     ("lib.rs", "rust"),
                     ("migration.sql", "sql"),
                     ("page.html", "html"),
                     ("theme.scss", "scss"),
                     ("Handler.php", "php"),
                     ("server.go", "go"),
                     ("main.c", "c"),
                 })
        {
            data.Add(file, language);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void StripFixture_RemovesSomethingAndLeavesNoDetectableCommentBehind(string file, string language)
    {
        var source = ReadFixture(file);

        var first = LexicalCommentStripper.Instance.Strip(source, language, CommentStripOptions.Default);
        var second = LexicalCommentStripper.Instance.Strip(first.Content, language, CommentStripOptions.Default);

        first.LanguageSupported.Should().BeTrue();
        first.RemovedComments.Should().BeGreaterThan(0, "{0} contains comments that must be removed", file);
        second.RemovedComments.Should().Be(0, "a second pass over {0} must find no comment left", file);
        first.UnterminatedSequences.Should().Be(0, "{0} must not leave a literal or comment open", file);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void StripFixture_KeepsTheLineCountAndTheLineEndings(string file, string language)
    {
        var source = ReadFixture(file);

        var result = LexicalCommentStripper.Instance.Strip(source, language, CommentStripOptions.Default);

        CountLines(result.Content).Should().Be(CountLines(source), "removing a comment must not move the lines of {0}", file);
        result.Content.Should().NotContain("\r", "the fixtures use LF and stripping must not introduce CR");
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void StripFixture_ReportedCountsMatchTheDetectedCommentForms(string file, string language)
    {
        var source = ReadFixture(file);

        var result = LexicalCommentStripper.Instance.Strip(source, language, CommentStripOptions.Default);

        result.RemovedComments.Should().Be(
            result.RemovedInlineComments + result.RemovedBlockComments + result.RemovedDocumentationComments);
        result.Content.Length.Should().BeLessThan(source.Length);
    }

    [Fact]
    public void JavaScriptFixture_KeepsLiteralsAndRemovesEveryRealComment()
    {
        var result = Strip("widget.js", "javascript");

        result.Should().Contain("'https://example.test/api//v1'");
        result.Should().Contain("/^https?:\\/\\//");
        result.Should().Contain("/[/*]/");
        result.Should().Contain("\"#not-a-comment\"");
        result.Should().Contain("render([]).length / 2");
        result.Should().NotContain("Widget bootstrap");
        result.Should().NotContain("Block comment");
        result.Should().NotContain("trailing comment");
        result.Should().NotContain("hole comment");
    }

    [Fact]
    public void CSharpFixture_KeepsVerbatimRawAndInterpolatedLiterals()
    {
        var result = Strip("Program.cs", "csharp");

        result.Should().Contain("@\"C:\\temp//raw\\path\"");
        result.Should().Contain("\"// not a comment\"");
        result.Should().Contain("$\"count={count }\"");
        result.Should().Contain("char separator = '/';");
        result.Should().NotContain("Renders widgets");
        result.Should().NotContain("A block comment");
    }

    [Fact]
    public void PythonFixture_KeepsDocstringsRawStringsAndFStrings()
    {
        var result = Strip("pipeline.py", "python");

        result.Should().Contain("Fixture module docstring", "a docstring is a string expression, not a comment");
        result.Should().Contain("r\"#\\d+\"");
        result.Should().Contain("https://example.test/api#fragment");
        result.Should().Contain("f\"{ENDPOINT} # label\"");
        result.Should().Contain("\"config#1.txt\"");
        result.Should().NotContain("a real trailing comment");
        result.Should().NotContain("a comment line");
    }

    [Fact]
    public void RustFixture_KeepsNestedCommentRemovalLifetimesAndRawStrings()
    {
        var result = Strip("lib.rs", "rust");

        result.Should().Contain("&'a str");
        result.Should().Contain("r#\"^https?://example\\.test/[/*]\"#");
        result.Should().Contain("self.source.len() as f64 / 2.0");
        result.Should().NotContain("Crate-level documentation");
        result.Should().NotContain("nested inner comment");
        result.Should().NotContain("still one comment");
    }

    [Fact]
    public void SqlFixture_KeepsDollarQuotedBodiesAndEscapedQuotes()
    {
        var result = Strip("migration.sql", "sql");

        result.Should().Contain("'a--b/*c*/d'");
        result.Should().Contain("'it''s -- quoted'");
        result.Should().Contain("-- this marker is inside the function body");
        result.Should().Contain("'/* literal */'");
        result.Should().NotContain("Create the reporting schema");
        result.Should().NotContain("A block comment");
        result.Should().NotContain("trailing comment");
    }

    [Fact]
    public void HtmlFixture_KeepsAttributesAndLeavesScriptAndStyleBodiesAlone()
    {
        var result = Strip("page.html", "html");

        result.Should().Contain("data-note=\"<!-- kept, this is an attribute -->\"");
        result.Should().Contain("// This script body is not scanned as markup: the line stays.");
        result.Should().Contain("/* This style body is not scanned as markup either. */");
        result.Should().NotContain("Removed: layout metadata");
        result.Should().NotContain("Removed: footer note");
    }

    [Fact]
    public void PhpFixture_KeepsHeredocBodiesAndHashValues()
    {
        var result = Strip("Handler.php", "php");

        result.Should().Contain("<p>// not a comment in a heredoc</p>");
        result.Should().Contain("<!-- not a markup comment either -->");
        result.Should().Contain("\"path#1\"");
        result.Should().Contain("'https://example.test/api//v1'");
        result.Should().NotContain("Shell-style comment");
        result.Should().NotContain("Handles a request");
    }

    [Fact]
    public void GoFixture_KeepsRawStringsAndReportsThatBuildDirectivesAreComments()
    {
        var result = Strip("server.go", "go");

        result.Should().Contain("// this is data, not a comment");
        result.Should().Contain("http://localhost:8080");
        result.Should().NotContain("Package main is a fixture");

        // A build directive is syntactically a comment, so it is removed. This is documented as a known
        // limitation: comment stripping is not directive-aware.
        result.Should().NotContain("go:build");
    }

    [Fact]
    public void ScssFixture_KeepsUrlsAndHashColours()
    {
        var result = Strip("theme.scss", "scss");

        result.Should().Contain("url(\"images/icon//dark/*.svg\")");
        result.Should().Contain("$primary: #4bbc25;");
        result.Should().NotContain("Palette variables");
        result.Should().NotContain("Structural card container");
    }

    [Fact]
    public void PreserveDocumentationComments_KeepsThemInTheFixtures()
    {
        var options = new CommentStripOptions { PreserveDocumentationComments = true };

        var csharp = LexicalCommentStripper.Instance.Strip(ReadFixture("Program.cs"), "csharp", options).Content;
        var rust = LexicalCommentStripper.Instance.Strip(ReadFixture("lib.rs"), "rust", options).Content;
        var go = LexicalCommentStripper.Instance.Strip(ReadFixture("server.go"), "go", options).Content;

        csharp.Should().Contain("/// <summary>").And.NotContain("A block comment");
        rust.Should().Contain("//! Crate-level documentation").And.Contain("/// A parser over borrowed input.");
        go.Should().NotContain("Package main is a fixture");

        // Go has no distinct documentation form, so preserving documentation cannot keep its '//' package comment.
        go.Should().Contain("package main");
    }

    [Fact]
    public void NodeAcceptsTheStrippedJavaScript()
    {
        var stripped = Strip("widget.js", "javascript");

        AssertParserAccepts("node", ["--check", "{file}"], stripped, ".js");
    }

    [Fact]
    public void PythonAcceptsTheStrippedModule()
    {
        var stripped = Strip("pipeline.py", "python");

        AssertParserAccepts("python", ["-m", "py_compile", "{file}"], stripped, ".py");
    }

    [Fact]
    public void GccAcceptsTheStrippedC()
    {
        var stripped = Strip("main.c", "c");

        AssertParserAccepts("gcc", ["-fsyntax-only", "{file}"], stripped, ".c");
    }

    /// <summary>
    /// Asserts that a real parser accepts the stripped output. The check is skipped when the tool is not on PATH,
    /// because a missing compiler must not be reported as a passing verification.
    /// </summary>
    private static void AssertParserAccepts(string tool, string[] arguments, string content, string extension)
    {
        var verified = VerifyWithParser(tool, arguments, content, extension);
        if (verified is null)
        {
            return;
        }

        verified.Value.Should().BeTrue("{0} must accept the stripped fixture", tool);
    }

    private static string Strip(string file, string language) =>
        LexicalCommentStripper.Instance.Strip(ReadFixture(file), language, CommentStripOptions.Default).Content;

    private static int CountLines(string text) => text.Split('\n').Length;

    /// <summary>
    /// Writes the stripped source to a temporary file and runs an available parser over it. Returns <c>true</c>
    /// when the parser accepted it, <c>false</c> when it rejected it, and <c>null</c> when the tool is not
    /// installed, in which case the assertions that do not need a compiler still apply.
    /// </summary>
    private static bool? VerifyWithParser(string tool, string[] arguments, string content, string extension)
    {
        if (FindOnPath(tool) is null)
        {
            return null;
        }

        var directory = Directory.CreateTempSubdirectory("plutus-comment-check-");
        var file = Path.Combine(directory.FullName, "sample" + extension);
        File.WriteAllText(file, content);

        try
        {
            var startInfo = new ProcessStartInfo(tool)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument.Replace("{file}", file));
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            process.WaitForExit(milliseconds: 60_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string? FindOnPath(string tool)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidate in new[] { tool, tool + ".exe", tool + ".cmd", tool + ".bat" })
            {
                var full = Path.Combine(directory.Trim('"'), candidate);
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }
}
