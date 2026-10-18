# Adding a parameter

This page is the recipe for extending the command line with a new `bundle` option that can also be set from
`.plutusrc` and reaches the pipeline as a field of `RunAppOptions`. It is written for the case that covers almost
every new option; it names the exact files, the exact methods, and the tests that must be updated so nothing
silently drifts.

The recipe is written around one example — `--max-line-length <int>`, a hypothetical limit on the length of a line
in the bundle — but the same steps apply to a `bool` flag, a `string`, an enum, a `FileSize`, a `TimeSpan`, or a
list of patterns. The variation points are called out in [Choosing the resolver method](#5-resolve-the-value-in-runoptionsresolver)
and [Common variants](#common-variants).

## The flow of one option

```
user typed --max-line-length 120
        │  BundleOptions.cs        declares the System.CommandLine option
        ▼
BundleCommand.Request()             CommandLineReader reads "stated or not stated"
        │  RunOptionsRequest.cs    nullable field: null means "this source said nothing"
        ▼
RunOptionsResolver.Build()          CLI > profile > built-in default, with provenance
        │  PlutusProfile.cs        the key of .plutusrc
        ▼
RunAppOptions.cs                    the final, non-nullable value the pipeline reads
        │
        ▼
PlutusBundler / BundleLimits / …
```

Each arrow is one edit. Nothing else in the codebase has to change, because the containers, the report and the
profile scaffolder already work from the types (`RunAppOptions`, `PlutusProfile`) by reflection or by reading the
resolved value.

## 1. Declare the option in `BundleOptions`

`Plutus.CLI/Commands/Bundle/BundleOptions.cs` owns the command-line surface. Add a property of type `Option<T>`,
give it a description, a default factory and, where useful, a validator.

```csharp
public Option<int> MaxLineLength { get; } = new("--max-line-length")
{
    Description = "Maximum line length in the bundle; longer lines are truncated. 0 means no limit.",
    DefaultValueFactory = _ => 0,
    Validators =
    {
        result =>
        {
            if (result.GetValueOrDefault<int>() < 0)
            {
                result.AddError("--max-line-length must be zero or greater.");
            }
        }
    }
};
```

Then list it in `Options()` so it appears in `--help` in a deliberate place:

```csharp
yield return MaxLineLength;
```

A short alias is optional and goes in `WithAliases()`:

```csharp
MaxLineLength.Aliases.Add("-l");
```

> **Why a default factory even though the value is nullable downstream.** `CommandLineReader.Value(...)` is documented
> to return the option's default when the flag was not typed. The `DefaultValueFactory` is what that default is; the
> "was it stated" distinction is asked for explicitly with `ExplicitValue`/`Explicit` (next step). Do not remove the
> factory to make `Value` return null — that would change what `--help` says and what a direct `Value` read yields.

## 2. Read the flag in `BundleCommand.Request()`

`Plutus.CLI/Commands/Bundle/BundleCommand.cs` builds a `RunOptionsRequest` from what the user actually typed. The
distinction that matters for a profile is *stated vs not stated*, and `CommandLineReader` already exposes it.

Add one line to the object initializer inside `Request(...)`:

```csharp
MaxLineLength = reader.ExplicitValue(options.MaxLineLength),
```

Choose the reader method by the shape of the option:

| Option shape | Reader method | Result type | Example in the codebase |
|---|---|---|---|
| `Option<int>` / `Option<bool>` / `Option<SomeEnum>` | `reader.ExplicitValue(...)` | `T?` | `MaxConcurrency`, `CommentStrategy` |
| `Option<string>` / `Option<List<string>>` | `reader.Explicit(...)` | `T?` | `Format`, `ExcludeDirs` |
| A `bool` option that means the negation of the value (e.g. `--no-gitignore`) | `reader.ExplicitInverse(...)` | `bool?` | `UseGitignore` |
| `Option<FileSize>` | `reader.ExplicitValue(...)` | `FileSize?` | `MaxFileSize` |
| `Argument<T>` (a positional argument) | `reader.Argument(...)` or `reader.Value(...)` | `T` | `ProjectPath` |

`Explicit*` returns `null` when the option was not typed, which is what the next step needs.

## 3. Add the field to `RunOptionsRequest`

`Plutus.Domain/Configuration/RunOptionsRequest.cs` is "what every source asked for". Its fields are nullable by
design: `null` means *this source said nothing*, which is what lets the resolver tell "unset" from "set to the
default".

```csharp
/// <summary>Maximum line length in the bundle; 0 means no limit. From --max-line-length or the profile.</summary>
public int? MaxLineLength { get; init; }
```

The type is chosen to match what the source can express:

| The option means | Type here |
|---|---|
| A non-negative integer | `int?` |
| A toggle | `bool?` |
| A free-form string (validated in the resolver) | `string?` |
| A size such as `1M` | `FileSize?` |
| A list of patterns | `IReadOnlyList<string>?` |
| A duration | `TimeSpan?` |

## 4. Add the key to `PlutusProfile`

`Plutus.Domain/Configuration/PlutusProfile.cs` is the `.plutusrc` schema. The key's JSON name is produced by
`JsonNamingPolicy.CamelCase`, and the loader derives the accepted key set by reflection (`KnownKeys()`), so there is
nothing to register.

```csharp
/// <summary>Maximum line length in the bundle; 0 means no limit. Equivalent to --max-line-length.</summary>
public int? MaxLineLength { get; init; }
```

> **Sizes and durations are stored as strings here**, not as `FileSize`/`TimeSpan`. JSON has no type for them, so
> `MaxFileSize` is a `string?` and `MaxDownloadSize` is a `string?`. The resolver parses the string and produces the
> typed value, with the profile path in the error message when parsing fails.

## 5. Resolve the value in `RunOptionsResolver`

`Plutus.Domain/Configuration/RunOptionsResolver.cs`, inside `Build(...)`, is where CLI, profile and default are
merged, and where the `Provenance` is recorded. The value returned here is what `RunAppOptions` carries.

```csharp
MaxLineLength = ResolveValue("maxLineLength", request.MaxLineLength, profile?.MaxLineLength, 0, provenance),
```

`ResolveValue` implements the entire precedence rule and records the provenance:

1. If `request.MaxLineLength` is not `null`, it wins. The key is recorded as overridden if the profile also stated
   it, otherwise it is simply used.
2. Otherwise, if `profile?.MaxLineLength` is not `null`, it wins over the default. It is recorded in
   `AppliedKeys` if it differs from the fallback, or in `DefaultedKeys` if it merely restates the default.
3. Otherwise the fallback (`0` here) applies and nothing is recorded.

That is the block `RunReportWriter` prints as:

```text
Configuration
  profile  /repo/.plutusrc — applied: maxLineLength; overridden by the command line: format
```

### Choosing the resolver method

The resolver offers one method per shape, so that every option has exactly one idiomatic call site and the
provenance bookkeeping cannot be forgotten:

| Value shape | Method | Example call |
|---|---|---|
| `struct` (`int`, `bool`, enum) | `ResolveValue<T>` | `ResolveValue("maxLineLength", request.MaxLineLength, profile?.MaxLineLength, 0, provenance)` |
| `string` with a fallback | `ResolveString` | `ResolveString("format", request.Format, profile?.Format, DefaultFormat, provenance)` |
| `string?` without a fallback | `ResolveNullableString` | `ResolveNullableString("branch", request.Branch, profile?.Branch, null, provenance)` |
| `List<string>` | `ResolveList` | `ResolveList("excludeDirs", request.ExcludeDirs, profile?.ExcludeDirs, provenance)` |
| `FileSize` | `ResolveMaxFileSize`, or the same pattern (`ResolveFileSize`) if it is a new size | `ResolveFileSize("maxDownloadSize", fromCommandLine?.MaxDownloadSize, profile?.MaxDownloadSize, fallback, provenance)` |
| `Encoding` | `ResolveEncoding` | `ResolveEncoding(request.Encoding, profile?.Encoding, provenance)` |

> **Manual provenance is only for merged keys.** `ResolveIncludeFilePatterns` is the one key that combines two
> profile entries (`includeFiles` and `includeExtensions`) into a single list, so it records both. `ResolveOutput`
> resolves the relative output path against the profile's directory, so it records that too. Unless a key needs that
> kind of logic, the standard `Resolve*` methods are all that is needed.

> **Failure to parse is a `ConfigurationException`, not a crash.** If the key holds a size or a duration that the
> value's `Parse` cannot accept, the resolver catches the parse error and re-throws it as a
> `ConfigurationException` whose message names the profile path and the key. `Resolve` turns that into a
> `RunOptionsResolution` with an error, and `BundleCommand` returns exit code `1` before reading any file. This is
> the pattern `ResolveMaxFileSize` and `ResolveFileSize` already follow.

## 6. Set the starter value in `ProfileTemplate`

`Plutus.Domain/Configuration/ProfileTemplate.cs` is what `plutus init` writes. Add the value to `Starter`, using the
same value as the built-in default:

```csharp
public static PlutusProfile Starter { get; } = new()
{
    // ...
    MaxLineLength = 0,
    // ...
};
```

This is not optional. `ProfileTemplateTests.Template_ContainsEveryKeyTheLoaderAccepts` enumerates the writable
properties of `PlutusProfile` by reflection and fails if any is missing from the template. And
`Template_StatesWhatARunWouldUseAnyway` resolves the starter profile and the same request without a profile and
asserts they produce the same `RunAppOptions`; if the starter value differs from the resolver's default, the
generated profile would change what a run produces, and that test would fail — which is the correct failure.

## 7. Add the field to `RunAppOptions` if the pipeline uses it

If the value is consumed by the pipeline (`PlutusBundler`, `BundleLimits`, `CodeMinifierFactory`,
`GitDiffOptions`, or any writer), it belongs on `Plutus.Domain/RunAppOptions.cs`. The property is `required` when
every caller is expected to set it — which is the case for anything that reaches `Build(...)`.

```csharp
public required int MaxLineLength { get; init; }
```

If the field is only a preference that the CLI needs for a message and nothing else consumes it, it can still go on
`RunAppOptions` for the report (`BundleRunReport` is separate) to read from, because `RunReportWriter` already
receives the options.

`RunAppOptions.Validate()` is the place for cross-field rules — "cannot be combined with X", "must be positive when
Y is set". A rule for one field alone is better placed in `BundleOptions.Validators` (so `--help`-driven parsing
rejects it) and in `PlutusProfileLoader.Validate` (so a `.plutusrc` typo is reported with the profile path). The
existing rules for `MaxConcurrency < 1` and `MaxTokens <= 0` are in both places for exactly that reason.

## 8. Validate the profile value (when there is a rule to state)

`Plutus.Domain/Configuration/PlutusProfileLoader.cs`, method `Validate(PlutusProfile, string path)`. Add a check for
the new key when the value can be present but meaningless:

```csharp
if (profile.MaxLineLength is < 0)
{
    return $"profile '{path}': 'maxLineLength' must be zero or greater";
}
```

The check names the profile path, because the file is what the user is editing. Leaving the check out is not an
error at this step — the resolver will still apply the value — but the failure would then surface later, in a
context that does not know where the value came from.

## 9. Update the documentation

- `docs/CLI.md` — add a row to the "Options" table and, when the option has a non-obvious interaction, a short
  example under "Common recipes".
- `docs/CONFIGURATION.md` — add a row to the schema table in the section the key belongs to (`Path rules`,
  `Output preferences`, `Source processing`, or a new section), and a line to the `init` example when it is a key a
  new project is likely to want.

## 10. Update the tests

Each test file below exists to pin one link in the chain. If the new parameter is not covered in all of them, a
link is untested.

| Test file | What to add |
|---|---|
| `Plutus.Test/Cli/CommandLineReaderTests.cs` | Add the option to `Explicit_IsNullForAnOptionTheUserDidNotType_EvenWhenItHasADefault` and to `Explicit_ReturnsWhatTheUserTyped`, so the stated/unstated distinction is pinned for this option too. |
| `Plutus.Test/Configuration/PlutusProfileLoaderTests.cs` | Add the key to `FullProfile` and assert it in `Load_BindsEveryKey`. Add the key to `Load_LeavesAnUnstatedKeyUnstated_InsteadOfGuessingADefault`. If the key has a validation rule, add a case to `Load_RejectsAValueItCannotUse` (`[InlineData(json, key)]`). |
| `Plutus.Test/Configuration/RunOptionsResolverTests.cs` | Three tests, one per branch: default without a profile, the value applied from the profile (matching `Resolve_ProfileValues_ApplyWhenTheCommandLineSaysNothing`), and the command-line flag overriding the profile (matching `Resolve_ExplicitFlags_OverrideTheProfile`). Assert the `Provenance` in `Resolve_Provenance_SaysWhichKeysCameFromTheProfileAndWhichWereReplaced` at least once, so a future change to the resolver is caught. |
| `Plutus.Test/Configuration/ProfileTemplateTests.cs` | No edit needed if `Starter` matches the built-in default. If the value differs from the default, `Template_StatesWhatARunWouldUseAnyway` fails and its `DefaultedKeys`/`AppliedKeys` assertion must be updated to say which key decided the run. |
| Any bundle/limits/writer tests that consume the field | Only if the parameter changes behavior at a layer that already has a test. `MarkdownBundleWriterTests` and `StructuredBundleWriterDiffTests` cover the writer contract; `BundleLimits` has its own coverage in `TokenBudgetTests`. |

`ProfileTemplateTests.Template_ContainsEveryKeyTheLoaderAccepts` and `Template_ComesBackUnchangedThroughTheLoader`
both walk `PlutusProfile` by reflection, so a new property is checked automatically once it is on the profile and the
starter.

## What does *not* need to change

These points look like candidates for edits, but the existing design already covers them. Touching them is a sign
that a step above was skipped or done twice.

- `PlutusProfileLoader.KnownKeys()` and `FindUnknownKeys` — both derive the accepted key set from `PlutusProfile` by
  reflection. A typo in a profile is reported with the list of valid keys without any registration.
- `PlutusProfileLoader.Validate` — only add a check when the profile carries a rule the resolver cannot express
  ("this string is a size", "this path must not contain a directory separator"). A rule for the profile itself
  belongs here; the resolver still owns the value.
- `RunReportWriter` — prints `ProfileProvenance`, which the resolver fills. When the resolver was called through the
  standard `Resolve*` methods, nothing has to be printed by hand.
- `ServiceCollectionExt` — the value reaches the container through `RunAppOptions`, which the CLI has already
  registered. The container does not need to know about individual options.
- `ProfileScaffolder` — it writes `ProfileTemplate.Render()`, so the new key appears in a generated profile as soon
  as the starter was updated.
- `PlutusProfileLoader.SerializerOptions` — every option is already bound to a `JsonStringEnumConverter` and the
  camel-case naming policy; a new enum or string field works without registering a converter.

## Common variants

The recipe above is the shape for the common case. The table records the deltas for the shapes that differ.

### A `bool` flag

The value is `bool?` in `RunOptionsRequest` and `bool?` in `PlutusProfile`. The resolver call is
`ResolveValue("someToggle", request.SomeToggle, profile?.SomeToggle, fallback, provenance)`. A negative flag such as
`--no-gitignore` is read with `reader.ExplicitInverse` at step 2, so the profile's `useGitignore: false` is overridden
by typing `--no-gitignore`, and the `UseGitignore` field on `RunAppOptions` keeps its natural polarity.

### A list of patterns

The value is `IReadOnlyList<string>?` in both request and profile, resolved with
`ResolveList("theKey", request.TheKey, profile?.TheKey, provenance)`. The resolver returns `[]` when neither source
stated anything. A command-line list **replaces** the profile's list rather than adding to it — that is what makes
"the profile's exclusions apply, unless the command line states its own" a single, visible rule in the report.

### A `FileSize` or a duration

The profile field is `string?` (JSON has no `FileSize`), and the resolver parses it with `FileSize.Parse` or
`TimeSpan.Parse`, throwing `ConfigurationException` with the profile path in the message. `ResolveMaxFileSize` and
`ResolveFileSize` are the existing examples; copy the pattern for a new size. For a duration, `TimeoutOptions` shows
the shape of a `record` with several `TimeSpan` fields, which a profile states as strings and the resolver parses.

### A value read from two keys

`ResolveIncludeFilePatterns` merges `includeFiles` (patterns) and `includeExtensions` (extensions turned into
patterns) into the one list the scanner understands. A command-line list overrides both, and the resolver records
that both keys were overridden. The pattern to reuse: compute the value, then call `provenance.Apply(key, …)` or
`provenance.OverrideIfStated(key, …)` for each underlying key, so the report is honest about the fact that two profile
entries fed one resolved option.

### A value the pipeline consumes under a different type

If the pipeline wants the value under a type that is not what the CLI or the profile carry, the conversion belongs
in `Build(...)`, next to the `ResolveValue` call, not in the caller. `MaxFileSize` from a `string` profile value to a
`FileSize` on `RunAppOptions`, `MaxTokens` from `int?` to `TokenBudgetOptions?`, and `Diff` from `DiffRequest` to
`GitDiffOptions` are the existing examples of a small conversion that stays inside the resolver.

## Checklist

- [ ] `Plutus.CLI/Commands/Bundle/BundleOptions.cs` — option declared, added to `Options()`, alias if any.
- [ ] `Plutus.CLI/Commands/Bundle/BundleCommand.cs` — read in `Request(...)` with the correct `Explicit*` method.
- [ ] `Plutus.Domain/Configuration/RunOptionsRequest.cs` — nullable field.
- [ ] `Plutus.Domain/Configuration/PlutusProfile.cs` — nullable field (string for `FileSize`/`TimeSpan`).
- [ ] `Plutus.Domain/Configuration/RunOptionsResolver.cs` — `ResolveValue` / `ResolveString` / `ResolveList` /
  `ResolveFileSize` call in `Build(...)`.
- [ ] `Plutus.Domain/Configuration/ProfileTemplate.cs` — starter value equal to the built-in default.
- [ ] `Plutus.Domain/RunAppOptions.cs` — field when the pipeline consumes it, and a `Validate()` rule for
  cross-field constraints.
- [ ] `Plutus.Domain/Configuration/PlutusProfileLoader.cs` — additional check in `Validate(...)` when the profile
  carries a rule the resolver cannot express.
- [ ] `Plutus.Test/Cli/CommandLineReaderTests.cs` — stated/unstated pinned.
- [ ] `Plutus.Test/Configuration/PlutusProfileLoaderTests.cs` — binding, absence, and validation.
- [ ] `Plutus.Test/Configuration/RunOptionsResolverTests.cs` — default, profile, override, provenance.
- [ ] `docs/CLI.md` and `docs/CONFIGURATION.md` — the user-facing reference.
