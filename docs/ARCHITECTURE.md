# Architecture and processing pipeline

Plutus is organized as a .NET solution with separate CLI, domain, infrastructure, and test projects.

## Projects

| Project | Responsibility |
|---|---|
| `Plutus.CLI` | Defines command-line arguments/options, validates CLI-level constraints, creates the service provider, and starts the application runner. |
| `Plutus.Domain` | Owns bundle workflow contracts, project-tree/scanning logic, ignore/comment-stripping/minification abstractions, token estimation/policy, and Markdown/JSON/XML writers. |
| `Plutus.Infrastructure` | Supplies physical filesystem/path services, concrete ignore matchers, remote repository adapters, and dependency-injection registration. |
| `Plutus.Test` | Unit tests for scanners, writers, filters, comment strippers, minifiers, path/remote resolution, and token-budget behavior. |

## High-level pipeline

```mermaid
flowchart TD
    A[CLI arguments] --> B[Resolve local paths or remote repositories]
    B --> C[Create file providers and project roots]
    C --> D[Resolve diff scope via git when requested]
    D --> E[Scan paths and build ProjectNode trees]
    E --> F[Apply diff scope, built-in, gitignore, include and exclude filters]
    F --> G[Read eligible files with bounded concurrency]
    G --> H[Detect binary/unreadable content and apply optional comment stripping or minification]
    H --> I[Estimate source tokens when configured]
    I --> J[Apply fail, warn, or select policy]
    J --> K[Render Markdown, JSON, or XML]
    K --> L[Report the comment strategy that answered]
```

## Design notes

- **File-provider boundary:** Domain processing works through `IFileProvider`/`IFileInfo` abstractions rather than directly owning all filesystem operations. Infrastructure supplies the physical implementation.
- **Shared project tree:** The scanned `ProjectNode` graph feeds both directory-tree rendering and bundle serialization, reducing duplicate traversal and keeping tree status aligned with processing results.
- **Writer strategy:** `IBundleWriter` implementations own format-specific serialization while sharing common file reading, size checks, optional minification, token policy, and status propagation in `BaseBundleWriter`.
- **Remote-provider strategy:** `IRemoteRepository` adapters encapsulate provider-specific URL recognition, ref selection, archive URL construction, download, and extraction for GitHub, GitLab, and Bitbucket.
- **Token-budget strategy:** `ITokenBudgetApplier` isolates the optional budget policy. `NoOpTokenBudgetApplier` represents the disabled path; `TokenBudgetApplier` estimates and selects whole files when enabled.
- **Diff-scope strategy:** `IGitDiffContextProvider` reports what changed and returns `null` when scoping does not apply. A diff is split into one `DiffChunk` per file by `GitDiffChunker`, which is what lets the size and token limits act per change instead of discarding the whole set. `ChangedFileMapper` is the single place that turns repository-relative git paths into root-relative paths — for the chunk paths and for the paths inside the patch text — so a bundle rooted at a subdirectory reports only its own changes.
- **Content mode:** `IBundleWriter.IncludeFileContents` separates "review the change" (`--diff-only`: paths plus per-file diffs) from "bundle the changed source" (`--diff-from`: contents plus diffs). It is a writer concern, so the scanner, filters and token policy behave identically in both modes.
- **One budget, two payloads:** `TokenBudgetSelector` owns the whole-file selection rules, and both `TokenBudgetApplier` (content) and `DiffScopeLimiter` (diffs) use it, so `fail`, `warn`, `select` and the selection strategies cannot drift apart between the two paths. `DiffScopeLimiter` performs no I/O: the caller supplies file sizes.
- **Comment-stripping strategy:** `--strip-comments` and `--minify` share one pipeline. `ICommentStripper` is the seam: `LexicalCommentStripper` is a character scanner driven by `LanguageCommentSyntaxRegistry`, while `AntlrCommentChannelStripper` drops tokens on a grammar's comment channels and refuses to rewrite a file whose tokens do not tile the input. `CommentStrippingPlanner` resolves the requested strategy against `GrammarInventory` (the grammars the project actually maintains) before any file is read, and `CommentStrippingReport` records which strategy answered per language so the run can print it. A language with no syntax entry is never scanned with another language's rules.
- **Source-processing modes:** `SourceProcessingMode` distinguishes stripping comments from minifying: the whitespace pass, C# region removal, and the final trim happen only in `Minify` mode, which keeps `--strip-comments` byte-faithful outside the comments themselves.
- **Dependency injection:** `Plutus.Infrastructure` registers the concrete services consumed by the CLI runner.

## Error and omission semantics

A file can be absent from embedded source content for different reasons: it exceeds `--max-file-size`, is detected as binary, cannot be read, or is omitted by token-budget selection. These are distinct outcomes and should remain distinguishable in output status/skip metadata and tests.

A diff can be absent for the same reasons, and the diff sections state which one applied instead of dropping the change silently. Because each diff is its own chunk, an omission affects one file and never the whole change set.

## Testing approach

Tests use file-provider abstractions and test file-info implementations to exercise core behavior without requiring a real repository for every case. When changing pipeline behavior, prefer a focused unit test plus an end-to-end CLI test when the behavior crosses multiple layers.

Comment stripping is additionally tested against real-world fixtures under `Plutus.Test/Fixtures/Comments`; where a fixture's own parser is on `PATH` (`node`, `python`, `gcc`), the stripped output is handed to it so the result is verified by something other than the scanner under test.
