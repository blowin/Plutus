# Signature extraction

`--signatures-only` reduces source files to their structural surface: declarations stay, implementation bodies are
replaced with a structural terminator. This document states what the pass is, how it works, what it guarantees, and
where it stops.

## What it is — and what it is not

| | |
|---|---|
| **It is** | A real syntax-tree transformation. The file is parsed by the language's own parser, a visitor walks the parse tree, and each implementation body is replaced by rewriting its **token span**. |
| **It is not** | A brace-matching or text heuristic: what counts as a body is decided by the parse tree, so a `{` inside a string, a lambda in a field initializer, or a verbatim string full of braces cannot be mistaken for one. |
| **It is not** | A compiler or a semantic model. Names are not resolved, types are not checked, and references are not rewritten: only syntax is rewritten. |
| **It is not** | A formatter. Text outside the replaced spans is byte-identical, and the pass is idempotent. |

## How it works

```text
source → parser → parse tree → visitor (collects body spans) → token stream → rewriter → skeleton
```

1. **Parse.** The file is parsed with the language's parser into a syntax tree, together with its diagnostics.
2. **Visit.** A language-specific visitor (`CSharpSyntaxWalker` for C#) descends the tree looking for declarations
   with implementation bodies. It is the same role ANTLR's `*BaseVisitor` plays for a grammar: a visitor over a real
   parse tree, not a scan over text.
3. **Collect spans.** Each implementation body becomes an *inclusive token span* plus the text that replaces it. A
   span runs from the declaration's first token to the end of its body, so the whitespace and comments between the
   signature and the body are consumed and the terminator lands where the declaration ends.
4. **Rewrite.** `TokenStreamRewriter` applies the collected spans (`Replace`, `Delete`, `InsertBefore`,
   `InsertAfter` — the same contract ANTLR's `TokenStreamRewriter` offers over token indices). It refuses to run on
   a token stream that does not tile its source, and it throws rather than guess when two edits claim the same
   tokens; the provider catches that and leaves the file unchanged with the reason reported.

The token stream is lossless by construction: every token carries its own trivia, so concatenating the tokens
reproduces the input exactly. That invariant is checked before any edit is applied, and it is what makes "only the
bodies changed" a property of the implementation rather than a promise.

## Supported languages

| Language | Identifiers | Provider | Mechanism |
|---|---|---|---|
| C# | `csharp` | `roslyn-csharp` | Roslyn syntax tree (`Microsoft.CodeAnalysis.CSharp`) |

A language without a provider is passed through **unchanged**, and the run reports it (`without a provider=N`);
nothing is approximated with a text heuristic. Adding a language means implementing `ISignatureSkeletonProvider`
for it; the container discovers the implementation by interface, so no dispatch table has to be edited.

## What is cut, and what survives

| Declaration | Result |
|---|---|
| Method with a block body | `public int Add(int a, int b);` |
| Method, property, indexer or operator with `=>` body | `public int Add(int a, int b);` |
| Property or indexer accessor with a body | `get;` / `set;` / `init;` (auto-property syntax) |
| Event accessors with bodies | `add;` / `remove;` |
| Constructor, destructor, conversion operator | `public WidgetService(IEnumerable<Widget> widgets);` |
| Local function inside a body being removed | removed with that body (one edit, no overlap) |
| Interface members (`void Draw();`) | untouched |
| Abstract, `extern`, `partial` declarations | untouched |
| Auto-properties (`{ get; set; }`) | untouched |
| Delegates, enums, records with a primary constructor | untouched |
| Attributes, documentation comments, `#region` directives | untouched (they are part of the declaration or its trivia) |
| Generic parameters and `where` constraints | kept: `public string Describe<T>(T value) where T : notnull;` |
| Field and constant initializers | kept — see limitations |

Any comment that stood between a signature and its body is re-emitted **after** the terminator, so it is neither
lost nor able to swallow the terminator:

```csharp
public void Run() // starts the work
{
}
```

becomes

```csharp
public void Run(); // starts the work
```

## Guarantees

- **Only the spans change.** Attributes, modifiers, return types, names, parameter lists, generic parameters,
  constraints, base lists and trivia outside the replaced spans are byte-identical.
- **Idempotent.** Running the pass again changes nothing and cuts nothing: a signature has no body left to remove.
- **Line endings survive** outside the replaced spans: LF stays LF, CRLF stays CRLF.
- **Tolerant.** A file that does not parse cleanly still produces a skeleton; the diagnostic count is reported
  (`files with parse diagnostics=N`). When the parse tree is too damaged for a safe cut — a synthesised body with
  no braces of its own, or spans that conflict — the file is returned unchanged with the reason recorded.
- **Never silent.** Every run prints which provider answered, how many bodies of each kind were cut, and how many
  files had no provider.

## Interaction with the other flags

| Flags | Result |
|---|---|
| `--signatures-only` | bodies replaced, comments and whitespace untouched |
| `--signatures-only --strip-comments` | comments removed first, then bodies replaced |
| `--signatures-only --minify` | comments removed, bodies replaced, whitespace-only lines collapsed, result trimmed |
| `--signatures-only --preserve-doc-comments` | documentation comments are kept when comments are removed; signature extraction never removes comments by itself |
| `--signatures-only` for a language without a provider | the file is passed through unchanged and counted |

The order is fixed: **comments → signatures → whitespace**. A structural pass therefore always sees the text that
the comment pass produced, which is why the reported counts stay consistent between the two.

## Limitations

- **The skeleton is not compilable code.** A method declaration without a body (or `extern`/`abstract`/`partial`)
  is not valid C#, so the artifact is for reading and for model context, not for building. That is the documented
  trade of a signature skeleton, and the terminator is the structural `;` the format calls for.
- **Field and constant initializers are kept.** `const` fields must have a value, `static readonly` initializers are
  often the interface (a compiled regex, a lookup table), and removing them would either break the syntax or hide
  the value a reader came for. If initializers matter, follow with `--minify` or write a provider policy for it.
- **Only members are visited.** Top-level statements, statements inside a kept body, and lambdas are never
  rewritten: a body that is removed takes its contents with it, and everything else is left alone.
- **Comments inside a removed body are removed with it.** They are implementation detail; comments between a
  signature and its body are preserved, as described above.
- **C# only, today.** An ANTLR parse-tree visitor or a tree-sitter query would plug into the same seam
  (`ISignatureSkeletonProvider` + the token-stream rewriter); see
  [ANTLR grammar inventory](ANTLR_GRAMMAR_INVENTORY.md) for why no grammar-backed provider exists yet.

## Tests

- `Plutus.Test/Structure/TokenStreamRewriterTests.cs` — replace, delete, insert, overlap refusal, tiling checks.
- `Plutus.Test/Structure/CSharpSignatureSkeletonProviderTests.cs` — interfaces, abstract members, generics with
  constraints, expression bodies, accessor bodies, events, constructors/operators, records, attributes, doc
  comments, field initializers, CRLF, broken code, idempotence.
- `Plutus.Test/Structure/SignatureExtractionPipelineTests.cs` — the flag matrix, unknown languages, the run report,
  and that the provider set the container discovers matches the one the catalog lists.
- `Plutus.Test/Structure/SignatureExtractionFixtureTests.cs` — a realistic fixture
  (`Plutus.Test/Fixtures/Structure/Service.cs`) checked for preserved declarations, removed implementation text and
  idempotence.
