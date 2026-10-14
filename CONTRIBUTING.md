# Contributing to Plutus

Thanks for taking the time to improve Plutus. Small, focused pull requests are easier to review and safer to merge.

## Before opening an issue

- Search existing issues and pull requests for the same bug or proposal.
- For bugs, include the command used, input type (local/remote), relevant options, expected behavior, actual behavior, and a minimal reproduction. Redact private paths, source code, tokens, and credentials.
- For feature requests, explain the user problem and how success could be tested.

## Development setup

1. Install the .NET 10 SDK.
2. Clone the repository and open `Plutus.sln`.
3. Restore and validate the solution:

   ```bash
   dotnet restore Plutus.sln
   dotnet format Plutus.sln --verify-no-changes --no-restore
   dotnet build Plutus.sln -c Debug
   dotnet test Plutus.sln -c Debug
   ```

## Pull request expectations

- Keep changes scoped to one problem or feature where practical.
- Add or update tests for changed behavior, especially filtering, token-budget, serialization, and remote-provider behavior.
- Update README/docs when CLI flags, defaults, output schemas, supported inputs, or user-visible behavior changes.
- Preserve deterministic behavior where the project promises it; make ordering and omission semantics explicit.
- Avoid adding secrets, real customer source code, generated bundles, build output, or local IDE files.
- Do not claim a test/build passed unless it was run successfully. Include known limitations and any commands not run in the PR description.
- Run `dotnet format Plutus.sln --verify-no-changes --no-restore` before submitting.

## Commit and review guidance

Use a clear imperative summary, for example `Fix root-file status rendering` or `Document token budget behavior`. Explain the reason for non-obvious implementation choices. A maintainer may request changes or additional tests before merging.

## Security-sensitive changes

Changes to sensitive-file matching, archive extraction, remote URL handling, or output-path resolution should include explicit threat considerations and regression tests. Do not weaken a safeguard only to make a test pass without documenting the behavior change.
