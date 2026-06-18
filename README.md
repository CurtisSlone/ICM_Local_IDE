# icm - a local-model ICM host with a deterministic oracle

`icm` runs an **ICM**: a small, local language model used as a bounded *proposer*, paired with a
deterministic *oracle* that decides whether each proposal is acceptable. You point the host at a
folder (an "instance") that holds a knowledge base, table schemas, scripts, and workflows, and it
gives you a chat console, a small editor GUI, and an [MCP](https://modelcontextprotocol.io) server
over the same instance.

It is Windows-native and dependency-light: it builds with the C# compiler that ships in the box with
the .NET Framework (no SDK, no NuGet, no MSBuild) and talks to a local [Ollama](https://ollama.com)
over plain HTTP.

## The idea: propose, then verify

A small local model is unreliable when you ask it to *decide* things or to drive an open-ended
tool-calling loop. It is reliable when each call is narrow and its output is checked. So this host
splits every task into three roles with one trust line:

| Role | Trusted to | Not trusted to |
| --- | --- | --- |
| **Model** (the proposer) | pick from an enum, draft text, write one table row | be right on its own; choose what runs next |
| **Code** (the glue) | read files, run tools, sequence steps | (it has no judgment to misuse) |
| **Oracle** (the decider) | accept or reject a proposal, deterministically | have opinions |

The oracle here is a **schema-driven validator for tab-separated tables**: it checks column count
(the classic "a tab got added or dropped" corruption), types, numeric ranges, and enum membership.
Because the verdict is deterministic, a wrong proposal is *caught*, not trusted - and the model can
be sent the exact errors and asked to try again, within a bound.

Two guardrails keep this honest:

- **The console is a dispatcher, not a chat.** Each turn is one constrained classify call
  (`{intent, query}`); then code runs the chosen capability. The model never improvises a tool loop.
- **Tools are declared; the model only fills arguments.** A tool's command is authored in the
  instance, never invented by the model. *Which* tool runs is decided by an authored workflow or by a
  capable orchestrator (e.g. Claude over MCP) - never by an open local-model loop.

## What you get

Two Windows executables built from one shared codebase:

- **`icm.exe`** - a console CLI: open / chat / mcp / flow / validate / gen / selftest.
- **`icm-gui.exe`** - a small "VSCode-lite" GUI: a workspace file tree (add / rename / delete / edit,
  confined to the opened folder), a text editor with a line-number gutter and find/replace, and a
  chat panel that drives the same engine as the console.

Prebuilt binaries are included in this folder, so you can run it without building anything.

## Quick start

**Prerequisites:** Windows 10/11 (the .NET Framework 4.x it needs is already installed).
[Ollama](https://ollama.com) running locally is required for the model-backed features (`chat`,
`ask`, `propose`, `gen`); `validate`, `selftest`, and script tools need no model. By default the host
talks to `http://localhost:11434` and uses `qwen3-coder:latest` (generation) and `nomic-embed-text`;
change these per instance in `icm.config.json`, or override the URL with the `OLLAMA_URL` env var.

From this folder:

```
.\icm.cmd selftest                           # verify the deterministic core (no model needed)
.\icm.cmd open example-icm                    # load + summarize the bundled example instance
.\icm.cmd validate example-icm tasks          # run the oracle on a table -> PASS
.\icm.cmd validate example-icm tasks_broken   # -> FAIL, prints the 4 planted faults (exit code 2)
.\icm-gui.cmd example-icm                      # open the GUI on the example instance
.\icm.cmd chat example-icm                     # operator console (needs Ollama)
```

> **Why `.cmd` and not `.exe`?** Run the `.cmd` launchers, not the bare `.exe` files. On Windows 11
> with Smart App Control on, an unsigned downloaded `.exe` is blocked from running directly; the
> launchers load the program in-memory inside the already-trusted PowerShell, which Smart App Control
> allows. See [Running under Smart App Control](#running-under-smart-app-control). Tip: add this
> folder to your `PATH` and you can run `icm ...` / `icm-gui .` from anywhere.

## The GUI

```
.\icm-gui.cmd                  # opens empty; use File > Open Folder
.\icm-gui.cmd example-icm      # open straight into an instance
```

Open any folder as a workspace; the tree, editor, and file operations are confined to that root. When
the folder is an instance (it has an `icm.config.json`), the chat panel activates and the dispatcher's
step trace streams into the log.

### Keyboard shortcuts

| Key | Action | Key | Action |
| --- | --- | --- | --- |
| Ctrl+O | Open folder | Ctrl+G | Go to line |
| Ctrl+P | Quick Open (fuzzy file find) | Alt+Z | Toggle word wrap |
| Ctrl+N | New file | Ctrl++ / Ctrl+- / Ctrl+0 | Editor zoom in / out / reset |
| Ctrl+S | Save | F5 | Refresh tree |
| Ctrl+W | Close file | F8 | Validate current file (oracle on the editor buffer) |
| Ctrl+F / Ctrl+H | Find / Replace | Ctrl+Shift+E / Ctrl+L | Focus tree / chat |
| Ctrl+Z / Ctrl+Y | Undo / redo (editor) | Esc | Close Find / Quick Open |

File tree: `Del` delete, `F2` rename, `Enter` open. Chat input: `Enter` or `Ctrl+Enter` sends,
`Shift+Enter` inserts a newline.

## How the chat works

The chat looks like a conversation but is a constrained router underneath. Each turn:

1. **Conversation layer** (optional) - rewrites a follow-up like "now do the same for the other
   table" into a standalone request. Non-load-bearing: a bad rewrite only costs a wrong intent.
2. **Dispatcher** - one constrained call classifies the line into an intent and extracts the query.
3. **Capability** - code runs the chosen capability deterministically:
   - `ask` - answer a question, grounded in one knowledge-base entry.
   - `validate` - run the oracle on a named table.
   - `propose` - the proposer/oracle loop: the model proposes a new table row, the oracle validates
     it, and on failure the exact errors are fed back for a bounded repair. On success the GUI offers
     to insert the validated row into the table file (you review and save). If it can't converge, it
     reports the oracle's verdict instead of writing a bad row.
   - `make` - freeform generation that is not a table row.

## Commands

```
icm open  <dir>                 load + summarize an instance
icm chat  <dir>                 operator console (dispatcher; needs Ollama)
icm mcp   <dir>                 serve the instance over MCP (stdio)
icm flow  <dir> <name> [in...]  run an authored workflow (flows/<name>.json)
icm validate <dir> <table>      run the oracle on schemas/<table>.json + samples/<table>.txt
icm gen   <dir> <prompt...>     one raw generate call (smoke-test the model)
icm selftest                    check the deterministic core (oracle/json/tsv/paths; no model)
```

`OLLAMA_URL` overrides the instance's configured `ollama_url`.

## Build your own ICM (the instance contract)

An instance is just a folder. Copy `example-icm/` and edit the pieces you need - the host runs
whatever it finds, and every piece is optional.

```
my-icm/
  icm.config.json     name, domain, model seats, and the tools this instance exposes
  manifest.json       the routing index: {id, title, path, summary} per knowledge-base entry
  SYSTEM.md           operating rules injected into grounded answers
  kb/*.md             knowledge-base entries (one topic per file) - the grounding for `ask`
  schemas/<t>.json    a table schema: columns with type / required / min / max / enum values
  samples/<t>.txt     tab-separated data for table <t> (first line is the header)
  tools/*             scripts the host can run (declared in icm.config.json)
  flows/*.json        authored workflows
```

`icm.config.json` looks like:

```json
{
  "name": "my-icm",
  "domain": "what this instance is about",
  "models": { "generate": "qwen3-coder:latest", "dispatch": "qwen3-coder:latest", "embed": "nomic-embed-text" },
  "ollama_url": "http://localhost:11434",
  "tools": [
    { "name": "ask",      "kind": "kb_answer", "description": "Answer from the knowledge base." },
    { "name": "validate", "kind": "validate",  "description": "Validate a table against its schema." },
    { "name": "propose",  "kind": "propose",   "description": "Propose a validated new row." }
  ]
}
```

A directory with no `icm.config.json` still opens (sensible defaults plus its knowledge base), so you
can start with just a `manifest.json` and a `kb/` folder and grow from there.

## Tools

A tool lets the host run a script or command an instance provides. Declare it in `icm.config.json`
with a `command` (an argv array) or a `script` (a `.ps1` file under `tools/`):

```json
{
  "name": "table_stats", "kind": "command",
  "description": "Report row/column counts for a table.",
  "command": ["powershell","-NoProfile","-ExecutionPolicy","Bypass","-File","tools/table_stats.ps1","-Table","{table}"],
  "inputSchema": { "type": "object", "properties": { "table": { "type": "string" } }, "required": ["table"] },
  "timeout": 30
}
```

The host runs the command with the **instance folder as the working directory** (so `tools/...` and
`samples/...` resolve), substitutes `{arg}` placeholders from the call's arguments, optionally pipes
one argument to standard input (`"stdin": "argname"`), enforces `timeout` (seconds), and captures
stdout / stderr / exit code. The command is passed as an argv array (not a shell string), so there is
no shell-injection surface. The instance author writes the command; the caller only fills the
declared arguments.

## Flows (authored workflows)

A flow (`flows/<name>.json`) is an ordered list of nodes over a shared state "blackboard"; each node
declares the `inputs` it reads and the `outputs` it writes. The flow is the orchestrator - the model
proposes inside nodes but never decides what runs next. Node kinds:

- `route` request -> entry_id (constrained pick of a knowledge-base entry)
- `read` entry_id -> context (code reads the entry; no model)
- `generate` templated prompt -> text (`prompt` supports `{state}` substitution)
- `answer` request + context -> answer (grounded with `SYSTEM.md`)
- `propose` table + request -> row, ok (proposer -> oracle -> bounded repair)
- `validate` table [+ tsv] -> verdict, ok (the oracle)
- `tool` named tool + args -> output, ok (runs a command/script tool)

```json
{ "name": "answer", "description": "Grounded knowledge-base answer",
  "nodes": [
    { "id": "route",  "kind": "route",  "inputs": ["request"],            "outputs": ["entry_id"] },
    { "id": "read",   "kind": "read",   "inputs": ["entry_id"],           "outputs": ["context"] },
    { "id": "answer", "kind": "answer", "inputs": ["request", "context"], "outputs": ["answer"] }
  ] }
```

Run it with `icm flow <dir> <name> [input...]`, or expose it as a tool (`{"kind": "flow", "flow":
"answer"}`) so an MCP client can call it. `example-icm/flows/` has `answer` (route -> read -> answer)
and `stats` (a deterministic `tool` node, no model).

## Drive it from a frontier model over MCP

`icm mcp <dir>` serves the instance over stdio JSON-RPC. `tools/list` advertises the instance's tools
(with their input schemas) and `tools/call` runs them - command/script tools, the oracle, grounded
answers, the propose loop, and whole flows. This is the same engine the local console uses, exposed so
a capable orchestrator can sequence the tools while the local model keeps filling the narrow,
oracle-checked slots.

## Running under Smart App Control

Windows 11's Smart App Control blocks running unsigned, locally-built or freshly-downloaded `.exe`
files directly. It does **not** block in-memory managed execution inside the already-trusted,
Microsoft-signed PowerShell. The launchers in this folder use that: they read the program's bytes and
run them in-process.

- Use `icm.cmd` / `icm-gui.cmd` (or `run-cli.ps1` / `run-gui.ps1`). Running `icm.exe` / `icm-gui.exe`
  directly may be blocked.
- This is the user's own local program running on the user's own machine; Smart App Control still
  guards everything else.

## Build from source

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

This calls the in-box .NET Framework C# compiler (`csc.exe`, pre-Roslyn, so the code targets C# 5)
with no SDK, NuGet, or MSBuild, and writes `icm.exe` and `icm-gui.exe`. It globs `src\` recursively
and partitions by folder so each executable has exactly one entry point: the `Cli\` folder (console)
is excluded from the GUI build and the `Gui\` folder (WinForms) from the console build. Non-default
references: `System.Web.Extensions.dll` (JSON) for both, and `System.Windows.Forms.dll` +
`System.Drawing.dll` for the GUI. Verify a build with `.\icm.cmd selftest`.

## Project layout (`src/`)

One flat `namespace Icm`; folders are organizational.

```
Conventions.cs   the instance contract in one place: file/dir names + intent/tool/node kind constants
Json.cs          JSON parse/serialize + navigation + small object/schema builders
Model/           pure data: Config, Manifest, TableSchema, Flow, Results
Runtime/         the engine: Instance (sandboxed IO), Oracle, Tsv, ToolRunner, Ollama, Dispatcher, FlowEngine
Server/Mcp.cs    the MCP server (tools/list + tools/call)
Cli/             the console executable: Program, ConsoleChat, SelfTest
Gui/             the GUI executable (WinForms): Gui, Native
```

## Status

Working: instance loading, the oracle (validate / propose with bounded repair), grounded `ask`,
script/command tools, authored flows, the chat dispatcher, the GUI, and the MCP server. Not yet
implemented: embedding-based routing for large knowledge bases, cross-table reference checks in the
oracle, and token streaming (turns print on completion).

## License

No license file is included yet - add one before distributing.
