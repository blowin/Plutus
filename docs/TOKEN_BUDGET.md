# Token budget behavior

The optional token budget is intended to keep source content within a rough context-size target. It is a deterministic estimate, not a guarantee that a particular model tokenizer will report the same count.

## Basic usage

```bash
plutus ./src --max-tokens 100000
plutus ./src --max-tokens 100000 --token-overflow-policy warn
plutus ./src --max-tokens 100000 --token-overflow-policy select
plutus ./src --max-tokens 100000 --token-overflow-policy select --token-selection-strategy largest-first
```

`--max-tokens` must be a positive integer. If it is omitted, token-budget enforcement is disabled.

## Overflow policies

| Policy | Behavior |
|---|---|
| `fail` (default) | If estimated eligible source content exceeds the budget, print a per-file report and return an error without writing the bundle. |
| `warn` | Print a warning/report and continue with all eligible files, even if the estimate exceeds the requested budget. |
| `select` | Keep only complete files that fit the budget; omitted files are marked in output metadata/tree where the format supports it. Files are not truncated. |

## Selection strategies

These apply only when the policy is `select` and the budget is exceeded.

- `path-order` (default): consider paths in ordinal order and include a file when its estimate fits the remaining budget. This favors reproducibility and path order, not file importance.
- `largest-first`: consider larger estimated files first; ties are resolved by ordinal path order. This can maximize the number of source characters represented, but may exclude many smaller files.

Both strategies are deterministic for the same input paths, file contents, options, and estimates. Selection is file-granular: a file either fits completely or is omitted.

## Estimator and limitations

The current offline estimator is named `chars/4-v1`. It counts Unicode scalar values in each readable file after configured source processing (including optional minification) and rounds the estimate up per file. Treat it as a heuristic.

The estimate:

- covers readable source content, not Markdown headings, directory trees, file lists, JSON/XML metadata, or other bundle overhead;
- is not tied to a specific model or tokenizer;
- does not include files skipped before reading, such as files larger than `--max-file-size` or files detected as binary/unreadable;
- can differ from the actual token count used by an LLM provider;
- does not guarantee that the complete bundle fits a model context window.

Leave headroom for bundle metadata, prompts, system instructions, and conversation history. If a strict model-specific token limit matters, validate the final artifact with the tokenizer used by that model.

## Reading the report

When the candidate estimate exceeds the budget, Plutus reports each file's estimate and decision. A file can be included, omitted by the budget, or skipped for a separate reason (for example, size limit or binary/unreadable content). These categories are intentionally distinct: a file skipped for being binary is not a token-budget omission.
