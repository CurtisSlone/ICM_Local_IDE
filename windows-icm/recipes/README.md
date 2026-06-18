# recipes/

End-to-end task playbooks - "to do X: step 1, 2, 3," chaining patterns/snippets/tools into a finished
result. Each recipe names the patterns and snippets it uses and, where relevant, the `tool` it drives.

## Sub-folders (each becomes the entry's `group`)

- `file-io/`        - read/write text and binary, encodings, streams, find/patch bytes at an offset.
- `process-memory/` - enumerate processes/modules, read/write process memory via Win32 P/Invoke.
- `tooling/`        - drive an external CLI (dumpbin, sigcheck, a Sysinternals tool, Ghidra headless)
                      from C#/PowerShell and parse its output.
- `automation/`     - build scripts, packaging, scheduled/host-run automation.

## Workflow

Lead each file with an `<!--icm {...}-->` block, then `icm reindex windows-icm`. Manifest is generated.
A recipe that should be *executed* (not just read) becomes a flow in `flows/` that composes the host
primitives (`generate` -> `tool` -> `loop`); the recipe file is the human-readable companion.
