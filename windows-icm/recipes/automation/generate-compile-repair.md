<!--icm
{
  "id": "generate-compile-repair",
  "title": "Generate, compile, repair until it builds (the loop flow)",
  "doc_type": "recipe",
  "group": "automation",
  "summary": "How the host turns a weak local model into a reliable code generator: a loop flow that generates C#, compiles it with the csc_check oracle, and feeds the compiler errors back into the next attempt until exit 0.",
  "keywords": ["generate verify repair", "loop flow", "csc_check", "compile", "oracle", "assembly line", "write_csharp", "write_grounded", "self-correct"],
  "source": { "origin": "authored", "repo_path": "win_icm_code/windows-icm/flows/csharp.json", "note": "describes the host's own loop primitive and the bundled C# generation flows" }
}
-->
# Generate, compile, repair until it builds

This is the core reliability loop of the ICM: the model PROPOSES code, the compiler (a deterministic
ORACLE) decides, and the loop feeds failures back until the code builds. The model never has to be
right on the first try - it has to be right by the last iteration.

## The pieces (all already in this instance)

- The `csc_check` tool - compiles a C# file with the in-box csc at langversion 5 and returns OK or the
  diagnostics (the oracle).
- The `loop` flow node - repeats a body until a state key is truthy (`until: ok`) or `maxIterations`.
- The `generate` node - writes code from a prompt that includes the previous compiler errors.

## The shape

```
loop (until ok, max 4):
  generate  ->  code     (prompt includes {context} grounding and {output} = last errors)
  tool csc_check(code) -> output, ok   (ok = exit 0)
```

## The two bundled flows

- `flows/csharp.json` (tool `write_csharp`): `route` to one KB entry, `read` it, then the loop.
- `flows/write_grounded.json` (tool `write_grounded`): `route_many` to EVERY relevant entry, `read`
  them all, then the loop. Use this when the task spans several patterns/references.

Run it: `icm flow windows-icm write_grounded "<task>"` or call the `write_grounded` tool over MCP.

## Why it works

The prompt forbids the C#-6+ syntax the in-box csc rejects (see `csharp-5-subset`), grounds the model
in real signatures from the KB, and the compiler catches anything the model still gets wrong. Each
repair pass sees the exact diagnostics, so errors converge instead of recurring. The same shape works
for PowerShell via `ps_parse` (`flows/powershell.json`).
