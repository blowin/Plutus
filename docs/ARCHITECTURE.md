# Architecture and processing pipeline

Plutus is organized as a .NET solution with separate CLI, domain, infrastructure, and test projects.

## Projects

| Project | Responsibility |
|---|---|
| `Plutus.CLI` | Defines command-line arguments/options, validates CLI-level constraints, creates the service provider, and starts the application runner. |
| `Plutus.Domain` | Owns bundle workflow contracts, project-tree/scanning logic, ignore/minification abstractions, token estimation/policy, and Markdown/JSON/XML writers. |
| `Plutus.Infrastructure` | Supplies physical filesystem/path services, concrete ignore matchers, remote repository adapters, and dependency-injection registration. |
| `Plutus.Test` | Unit tests for scanners, writers, filters, minifiers, path/remote resolution, and token-budget behavior. |

## High-level pipeline

```mermaid
flowchart TD
    A[CLI arguments] --> B[Resolve local paths or remote repositories]
    B --> C[Create file providers and project roots]
    C --> D[Resolve diff scope via git when requested]
    D --> E[Scan paths and build ProjectNode trees]
    E --> F[Apply diff scope, built-in, gitignore, include and exclude filters]
    F --> G[Read eligible files with bounded concurrency]
    G --> H[Detect binary/unreadable content and apply optional minification]
    H --> I[Estimate source tokens when configured]
    I --> J[Apply fail, warn, or select policy]
    J --> K[Render Markdown, JSON, or XML]
```

## Design notes

- **File-provider boundary:** Domain processing works through `IFileProvider`/`IFileInfo` abstractions rather than directly owning all filesystem operations. Infrastructure supplies the physical implementation.
- **Shared project tree:** The scanned `ProjectNode` graph feeds both directory-tree rendering and bundle serialization, reducing duplicate traversal and keeping tree status aligned with processing results.
- **Writer strategy:** `IBundleWriter` implementations own format-specific serialization while sharing common file reading, size checks, optional minification, token policy, and status propagation in `BaseBundleWriter`.
- **Remote-provider strategy:** `IRemoteRepository` adapters encapsulate provider-specific URL recognition, ref selection, archive URL construction, download, and extraction for GitHub, GitLab, and Bitbucket.
- **Token-budget strategy:** `ITokenBudgetApplier` isolates the optional budget policy. `NoOpTokenBudgetApplier` represents the disabled path; `TokenBudgetApplier` estimates and selects whole files when enabled.
- **Diff-scope strategy:** `IGitDiffContextProvider` reports what changed and returns `null` when scoping does not apply. Parsing the diff text that is bundled anyway keeps path discovery and the reported `.diff` consistent. `ChangedFileMapper` is the single place that turns repository-relative git paths into root-relative paths, so a bundle rooted at a subdirectory reports only its own changes.
- **Content mode:** `IBundleWriter.IncludeFileContents` separates "review the change" (`--diff-only`: paths plus raw diff) from "bundle the changed source" (`--diff-from`: contents plus diff). It is a writer concern, so the scanner, filters and token policy behave identically in both modes.
- **Dependency injection:** `Plutus.Infrastructure` registers the concrete services consumed by the CLI runner.

## Error and omission semantics

A file can be absent from embedded source content for different reasons: it exceeds `--max-file-size`, is detected as binary, cannot be read, or is omitted by token-budget selection. These are distinct outcomes and should remain distinguishable in output status/skip metadata and tests.

## Testing approach

Tests use file-provider abstractions and test file-info implementations to exercise core behavior without requiring a real repository for every case. When changing pipeline behavior, prefer a focused unit test plus an end-to-end CLI test when the behavior crosses multiple layers.
