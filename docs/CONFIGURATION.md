# Configuration profiles (`.plutusrc`)

A profile is a small file at the repository root that states the defaults for a project, so the usual command line
stops being a list of exclusions:

```bash
# Without a profile
dotnet run --project Plutus.CLI -- . --exclude-dirs bin obj node_modules dist --exclude-files "*.g.cs" \
  --include-files "*.cs" "*.razor" --format md -o artifacts/context.md

# With one
dotnet run --project Plutus.CLI -- .
```

## Start from a template

Nobody has to learn the schema by heart: `init` writes the whole profile with every key commented out, each one next
to its command-line equivalent and its default.

```bash
dotnet run --project Plutus.CLI -- init
```

```text
Profile template written: /repo/.plutusrc
Every key is commented out, so the file changes nothing until you uncomment what you need.
A run from this directory or below picks it up automatically; --no-profile ignores it.
```

The generated file is valid and inert: a run that finds it says so and reports that no key was used, so generating a
template cannot change what a bundle contains. Uncomment what the repository needs, delete the rest.

| Option | Default | Description |
|---|---|---|
| `-o`, `--output <path>` | `<current directory>/.plutusrc` | Where to write the template. |
| `--force` | Off | Replace an existing profile instead of refusing to touch it. |

## Where the profile is found

A run looks for `.plutusrc`, then `.plutusrc.json`, in this order:

1. the path named by `--profile <path>`, when given;
2. otherwise the path named by the `PLUTUS_PROFILE` environment variable, when set;
3. otherwise the nearest profile in the working directory or any parent directory, stopping at the git repository
   root — a profile kept *above* the repository belongs to whatever is above it and is deliberately not picked up.

`--no-profile` disables all three, so a run can always be reproduced with the command line alone. A profile that is
named explicitly but does not exist is an error, not an empty preset: a typo must not look like "this repository
wants the defaults". The same goes for a profile that cannot be read, is not valid JSON, or states a key Plutus does
not know.

The file is JSON. Comments (`//`, `/* */`) and trailing commas are accepted, so it reads like a configuration file;
`docs/CONFIGURATION.md` uses `.jsonc` examples for that reason. A YAML profile is **not** supported: the project
ships no YAML parser, and a hand-written one would be a liability for block scalars, anchors and merge keys. If YAML
is wanted, it should arrive as a deliberate dependency and a documented schema.

## Precedence

| Source | Wins over |
|---|---|
| An explicit command-line flag | the profile and the built-in default |
| A key stated by the profile | the built-in default |
| The built-in default of the option | — |

A flag **replaces** the profile's value for the same key rather than adding to it, and the run report states what
happened:

```text
Configuration
  profile  /repo/.plutusrc — applied: excludeDirs, includeExtensions; overridden by the command line: format
```

So `--exclude-dirs node_modules` in a repository whose profile lists `bin`, `obj`, `node_modules` and `dist` means
*only* `node_modules` is excluded for that run. Combining the two sources is a different feature (it needs its own
flag to be honest about it) and is not implemented.

An option that is a flag can also be turned off explicitly, which is what overrides a profile that enables it:

```bash
dotnet run --project Plutus.CLI -- . --minify false   # the profile says minify: true
```

## Schema

Every key is optional; a profile states only what it changes. Unknown keys are an error.

### Path rules

| Key | Type | Equivalent flag |
|---|---|---|
| `includeFiles` | string[] | `--include-files` |
| `includeDirs` | string[] | `--include-dirs` |
| `includeExtensions` | string[] | `--include-files` (see below) |
| `exclude` | string[] | `--exclude` |
| `excludeDirs` | string[] | `--exclude-dirs` |
| `excludeFiles` | string[] | `--exclude-files` |
| `useGitignore` | bool | `--no-gitignore` (inverted) |
| `useDefaultExcludes` | bool | `--use-default-excludes` |
| `allowDangerousFiles` | bool | `--allow-dangerous-files` |

`includeExtensions` is the friendly form of an inclusive filter: each entry becomes the pattern that selects it at
any depth, so `".cs"`, `"cs"` and `"*.cs"` all mean the same, and `[".cs", ".ts"]` is `--include-files "*.cs" "*.ts"`.
An entry that names a directory (`"src/*.cs"`) is rejected with a pointer at `includeFiles`, because that is the key
for patterns.

### Output preferences

| Key | Type | Equivalent flag |
|---|---|---|
| `output` | string | `--output` |
| `format` | `"md"` \| `"json"` \| `"xml"` | `--format` |
| `encoding` | string | `--encoding` |
| `maxFileSize` | string (`"512K"`, `"2M"`) | `--max-file-size` |
| `maxTokens` | positive integer | `--max-tokens` |
| `tokenOverflowPolicy` | `"fail"` \| `"warn"` \| `"select"` | `--token-overflow-policy` |
| `tokenSelectionStrategy` | `"path-order"` \| `"largest-first"` | `--token-selection-strategy` |
| `maxConcurrency` | integer ≥ 1 | `--max-concurrency` |
| `includeFileContents` | bool | `--diff-only` (inverted) |

A relative `output` is resolved against the **profile's own directory**, not the directory the command runs in: a
checked-in preset writes next to the repository it describes. An `output` given on the command line stays relative to
the working directory, as before.

### Source processing

| Key | Type | Equivalent flag |
|---|---|---|
| `minify` | bool | `--minify` |
| `stripComments` | bool | `--strip-comments` |
| `preserveDocComments` | bool | `--preserve-doc-comments` |
| `commentStrategy` | `"auto"` \| `"lexical"` \| `"antlr"` | `--comment-strategy` |
| `signaturesOnly` | bool | `--signatures-only` |

The comment strategy is still resolved against the maintained grammars before any file is read, so a profile cannot
make `antlr` succeed where the build ships no grammar.

What a profile cannot set: the project paths, `--branch` / `--commit`, and the diff scope (`--diff-only`,
`--diff-from`, `--diff-to`, `--diff-include-untracked`). Those identify *what* is being bundled rather than *how*, and
a preset that silently changed the target would be a trap.

## Example

```jsonc
{
  // .plutusrc at the repository root
  "excludeDirs": ["bin", "obj", "node_modules", "dist", ".vs"],
  "excludeFiles": ["*.g.cs", "*.Designer.cs", "*.min.js"],
  "includeExtensions": [".cs", ".razor", ".json"],

  "output": "artifacts/context.md",
  "format": "md",
  "maxFileSize": "2M",
  "maxTokens": 200000,
  "tokenOverflowPolicy": "select",
  "tokenSelectionStrategy": "path-order",

  "stripComments": true,
  "preserveDocComments": true
}
```

## Failure messages

A profile problem names the file and the key, and stops the run before anything is read:

```text
Error: profile '/repo/.plutusrc' has unknown key(s): 'excludeDir'. Known keys are: allowDangerousFiles,
commentStrategy, encoding, exclude, excludeDirs, excludeFiles, format, includeDirs, includeExtensions,
includeFileContents, includeFiles, maxConcurrency, maxFileSize, maxTokens, minify, output, preserveDocComments,
signaturesOnly, stripComments, tokenOverflowPolicy, tokenSelectionStrategy, useDefaultExcludes, useGitignore.
```

## Tests

- `Plutus.Test/Configuration/PlutusProfileLoaderTests.cs` — binding of every key, comments and trailing commas,
  unknown keys, wrong types, malformed JSON, a missing explicit path, and the search: nearest wins, walk up, stop at
  the repository root, disable.
- `Plutus.Test/Configuration/RunOptionsResolverTests.cs` — defaults without a profile, profile values applied,
  explicit flags overriding (including turning a flag off), inclusive patterns from extensions, the profile-relative
  output path, token budget and diff limits from the profile, and the provenance the report prints.
- `Plutus.Test/Configuration/ProfileTemplateTests.cs` — the generated template documents every key the loader
  accepts and loads as a profile that sets nothing.
- `Plutus.Test/Configuration/ProfileScaffolderTests.cs` — the write: template content, no byte-order mark, creating
  the directory, refusing to replace an existing profile, `--force`, and a write failure reported as one.
- `Plutus.Test/Diagnostics/RunReportWriterTests.cs` — the `Configuration` block, and that a run without a profile
  prints none.
