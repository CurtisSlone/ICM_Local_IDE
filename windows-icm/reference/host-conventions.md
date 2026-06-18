<!--icm
{
  "id": "host-conventions",
  "title": "ICM host conventions (tools, flows, schemas)",
  "doc_type": "reference",
  "summary": "How this instance's pieces fit the host: declaring a command/script tool, authoring a flow (incl. the loop node), and the table-schema oracle. Read before adding a tool or flow.",
  "keywords": ["tool", "flow", "schema", "oracle", "node kind", "loop", "branch", "search", "command", "script", "manifest", "config", "instance contract"],
  "source": { "origin": "authored", "note": "this host's instance contract - mirrors CLAUDE.md / README in win_icm_code" }
}
-->
# ICM host conventions (tools, flows, schemas)

How this instance's pieces fit the host that runs it.

## A tool (run a script/command)
Declare it in `icm.config.json` under `tools`. Give a `command` (argv array) or a `script` (.ps1):
```json
{ "name": "csc_check", "kind": "command",
  "command": ["powershell","-NoProfile","-ExecutionPolicy","Bypass","-File","tools/csc_check.ps1"],
  "inputSchema": { "type":"object", "properties": { "code": {"type":"string"} }, "required": ["code"] },
  "stdin": "code", "timeout": 60 }
```
- The host runs it with the **instance folder as the working directory** (so `tools/...` resolves).
- `{arg}` tokens in the command are filled from the call's arguments; `"stdin": "code"` pipes that
  argument to the process's standard input instead (use this for large payloads like source code).
- It captures stdout/stderr and the exit code; **exit 0 = `ok`**.

## A flow (authored workflow)
`flows/<name>.json` is an ordered list of nodes over a shared state blackboard. Node kinds:
`route`, `read`, `generate`, `answer`, `propose`, `validate`, `tool`, `loop`, `branch`, and
`search`. A `loop` repeats a `body` until a state key is truthy (`until`) or `maxIterations` times -
this is how generate -> check -> repair is built (see `flows/csharp.json`). A `branch` runs its
`then` or `else` body from a `when`/`test` on a state key (`empty`/`nonempty`/`truthy`/`falsy`). A
`search` runs a hybrid BM25+embedding search over `refdocs/<corpus>.json` and writes the hits to
`context`; combine the two for tier-2 grounding - KB first, docs corpus as fallback (see
`flows/answer_fallback.json`). Reference state in prompts/args with `{key}`.

## The table oracle (optional)
For tabular data, add `schemas/<table>.json` (columns with `type`/`required`/`min`/`max`/enum
`values`) and `samples/<table>.txt` (tab-separated, first line = header). Then `validate` checks it
and `propose` can add an oracle-gated row. This instance is code-focused, so its "oracle" is the
compiler/parser (the `csc_check` / `ps_parse` tools), not a table schema - but the table oracle is
available if you add a data file.
