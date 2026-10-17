using System.CommandLine;

namespace Plutus.CLI;

/// <summary>
/// Reads a parse result the way configuration resolution needs it: "stated" and "not stated" are different answers,
/// because a profile may only fill in what the command line left alone. An option that was not typed still reports a
/// value — the one from its default factory — which is exactly why the distinction has to be asked for explicitly.
/// </summary>
internal sealed class CommandLineReader(ParseResult parseResult)
{
    /// <summary>The value of an option, whether or not the user stated it.</summary>
    public T Value<T>(Option<T> option) => parseResult.GetValue(option)!;

    /// <summary>The value of an argument, whether or not the user stated it.</summary>
    public T Argument<T>(Argument<T> argument) => parseResult.GetValue(argument)!;

    /// <summary>
    /// A reference-typed option as the user typed it, or <c>null</c> when only the option's default filled it in.
    /// </summary>
    public T? Explicit<T>(Option<T> option) where T : class? =>
        parseResult.GetResult(option) is { Implicit: false } ? parseResult.GetValue(option) : null;

    /// <summary>The same for a value-typed option, including flags such as <c>--minify</c>.</summary>
    public T? ExplicitValue<T>(Option<T> option) where T : struct =>
        parseResult.GetResult(option) is { Implicit: false } ? parseResult.GetValue(option) : null;

    /// <summary>
    /// The same for a nullable value-typed option (e.g., <c>FileSize?</c>, <c>int?</c>),
    /// returning <c>null</c> when only the default filled it in.
    /// </summary>
    public T? ExplicitNullableValue<T>(Option<T?> option) where T : struct =>
        parseResult.GetResult(option) is { Implicit: false } ? parseResult.GetValue(option) : null;

    /// <summary>The value of an option that is <c>false</c> by default and means "off" when typed.</summary>
    public bool? ExplicitInverse(Option<bool> option) =>
        ExplicitValue(option) is { } value ? !value : null;
}
