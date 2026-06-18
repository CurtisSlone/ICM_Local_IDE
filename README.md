# win_icm_code - the ICM host (C#)

A C# port of the Rust ICM host (`../ollama_ICM_code`). Same job: "open a directory and land in
the ICM." It loads an ICM instance and runs it - a chat-style dispatcher for a human operator,
an MCP server for a strong orchestrator (Claude), and the deterministic oracle both rely on. It
ships as two Windows-native executables over one shared core:

- `icm.exe` - the console CLI (open / chat / mcp / validate / gen).
- `icm-gui.exe` - a "VSCode lite" WinForms front end: a workspace file tree (add/delete/
  rename/read/edit, confined to the opened root), a text editor, and a chat panel that drives
  the same dispatcher the console uses.

This port targets the C# compiler that ships with Windows. No SDK, no NuGet, no MSBuild: it
builds with the in-box .NET Framework `csc.exe` and runs on the .NET Framework 4.x that is
already present on Windows 10/11. Non-default references: `System.Web.Extensions.dll` (the
`JavaScriptSerializer` JSON layer) for both; `System.Windows.Forms.dll` + `System.Drawing.dll`
(from the GAC) for the GUI.

## Build

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

This calls `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` directly with
`-noconfig -langversion:5` (the in-box compiler is pre-Roslyn and caps at C# 5) and writes both
`icm.exe` and `icm-gui.exe` next to the script. It globs `src\` recursively and partitions by
folder: the `Cli\` folder (console `Main`) is excluded from the GUI build and the `Gui\` folder
(WinForms + the GUI `Main`) from the console build, so each exe has exactly one entry point.

## Running under Smart App Control

This machine has Smart App Control (SAC) in enforce mode, which blocks running unsigned,
locally-built `.exe` files directly. The fix needs no SAC-off and no signing: run the assembly's
bytes **in-memory** inside the already-trusted `powershell.exe` (SAC gates PE files loaded from
disk, not in-memory managed execution). The launchers do this for you:

```
.\icm.cmd validate example-icm tasks         # console CLI (in-memory)
.\icm-gui.cmd example-icm                     # GUI (in-memory, STA host)
.\icm-gui.cmd .                              # open the current folder
```

Add `win_icm_code` to your PATH and you can run `icm ...` / `icm-gui .` from any directory (like
`code .`). Running `.\icm.exe` / `.\icm-gui.exe` directly will be blocked by SAC; use the `.cmd`
launchers (or `run-cli.ps1` / `run-gui.ps1`).

## GUI

```
.\icm-gui.cmd                       # open, then File > Open Folder
.\icm-gui.cmd example-icm           # open straight into a workspace
```

Open any folder as the workspace; the tree, editor, and file operations are confined to that
root. When the root contains `icm.config.json` the chat panel activates against that instance
(needs Ollama). The chat is the three-layer design from the project DEVLOG: a non-load-bearing
conversation layer (rewrites follow-ups into standalone requests), the constrained dispatcher
(one `{intent, query}` classify call), then the ICM agent + oracle. The dispatcher's step trace
streams into the chat log.

Chat intents: `ask` (grounded KB answer), `validate` (oracle on a named table), `make` (freeform
generate), and `propose` - the proposer/oracle edit loop: e.g. "add a level 6 paladin skill called
Holy Bolt" makes the model propose a `skills.txt` row, the oracle validates it (column count,
types, ranges, enums), bounded repair fixes failures, and on PASS the GUI offers to insert the
validated row into `samples/<table>.txt` (you review and Ctrl+S). If it can't converge, it reports
the oracle's diagnostics instead of shipping a bad row.

### Keyboard shortcuts

| Key | Action | Key | Action |
| --- | --- | --- | --- |
| Ctrl+O | Open folder | Ctrl+G | Go to line |
| Ctrl+P | Quick Open (fuzzy file find) | Alt+Z | Toggle word wrap |
| Ctrl+N | New file | Ctrl++ / Ctrl+- / Ctrl+0 | Editor zoom in / out / reset |
| Ctrl+S | Save | F5 | Refresh tree |
| Ctrl+W | Close file | F8 | Validate current file (oracle on editor buffer) |
| Ctrl+F / Ctrl+H | Find / Replace | Ctrl+Shift+E / Ctrl+L | Focus tree / chat |
| Ctrl+Z / Ctrl+Y | Undo / redo (editor) | Esc | Close Find / Quick Open |

File tree: `Del` delete, `F2` rename, `Enter` open. Chat input: `Enter` or `Ctrl+Enter` sends,
`Shift+Enter` inserts a newline. Find: `Enter` finds next.

## Commands

```
icm open  <dir>                 load + summarize an ICM instance
icm chat  <dir>                 operator console (dispatcher; needs Ollama)
icm mcp   <dir>                 serve this ICM over MCP (stdio)
icm flow  <dir> <name> [in...]  run an authored workflow (flows/<name>.json)
icm validate <dir> <table>      run the oracle on schemas/<table>.json + samples/<table>.txt
icm gen   <dir> <prompt...>     one raw generate call (smoke-test the model seat)
icm selftest                    check the deterministic core (oracle/json/tsv/paths; no model)
```

`OLLAMA_URL` overrides the config's `ollama_url`.

## Try it

```
.\icm.cmd open example-icm
.\icm.cmd validate example-icm tasks                 # PASS
.\icm.cmd validate example-icm tasks_broken          # FAIL (4 planted faults), exit code 2
.\icm.cmd chat example-icm                           # needs Ollama on localhost:11434
```

## Tools & MCP

An instance declares runnable tools in `icm.config.json`. Each tool has a `kind` the host knows
how to dispatch:

- `validate` - run the oracle on a table (`{table, tsv?}`).
- `kb_answer` - a grounded KB answer (`{question}`).
- `propose` / `generate_verify` - the proposer/oracle row loop (`{table, request}`).
- a **command/script tool** - any `kind` that declares a `command` (argv array) or a `script`
  (`.ps1`). Example:

```json
{
  "name": "table_stats", "kind": "command",
  "description": "Report row/column counts for a table.",
  "command": ["powershell","-NoProfile","-ExecutionPolicy","Bypass","-File","tools/table_stats.ps1","-Table","{table}"],
  "inputSchema": { "type":"object", "properties": { "table": {"type":"string"} }, "required":["table"] },
  "timeout": 30
}
```

The host runs the command with the **instance root as the working directory** (so relative paths
like `tools/...` and `samples/...` resolve), substitutes `{arg}` placeholders from the call's
arguments, optionally pipes one argument to stdin (`"stdin":"argname"`), enforces `timeout`
(seconds), and captures stdout/stderr/exit. Because the command is passed as an argv (not a shell
string), there is no shell-injection surface.

**The guardrail:** the command is authored by the instance; the model (or a flow) only fills the
declared arguments. Tool *selection* is done by the strong orchestrator (Claude over MCP) or by an
authored flow - never by an open local-model loop.

`icm mcp <dir>` serves these over stdio JSON-RPC: `tools/list` advertises each tool (using its
authored `inputSchema`), and `tools/call` dispatches by kind. Same server, two callers - Claude or
the local dispatcher.

## Flows (authored workflows)

A flow is `flows/<name>.json`: an ordered list of nodes, each declaring the `inputs` it reads from a
shared state blackboard and the `outputs` it writes. The flow is the orchestrator - the local model
proposes inside nodes but never decides what runs next. Node kinds:

- `route` request -> entry_id (constrained KB route)
- `read` entry_id -> context (code reads the KB entry; no model)
- `generate` templated prompt -> text (heavy seat; `prompt` with `{state}` substitution)
- `answer` request + context -> answer (grounded with `SYSTEM.md`)
- `propose` table + request -> row, ok (proposer -> oracle -> bounded repair)
- `validate` table [+ tsv] -> verdict, ok (the oracle)
- `tool` named tool + args -> output, ok (runs an instance command/script tool)

```json
{ "name": "answer", "description": "Grounded KB answer",
  "nodes": [
    { "id": "route",  "kind": "route",  "inputs": ["request"],            "outputs": ["entry_id"] },
    { "id": "read",   "kind": "read",   "inputs": ["entry_id"],           "outputs": ["context"] },
    { "id": "answer", "kind": "answer", "inputs": ["request", "context"], "outputs": ["answer"] }
  ] }
```

Run a flow with `icm flow <dir> <name> [input...]`, or expose one as a tool (`{"kind":"flow","flow":"answer"}`)
so Claude can call it over MCP. `example-icm/flows/` has `answer` (route->read->answer) and
`stats` (a deterministic `tool` node calling `table_stats`, no model).

## Adapting to any ICM

The host opens any instance directory. Model seats are read from nested `models.{generate,dispatch,
embed}`, falling back to the flat `model` / `embed_model` fields the Python ICMs use. A directory with
no `icm.config.json` still opens (defaults + KB only). So an instance is just: `icm.config.json`
(optional), `manifest.json` + `kb/` (grounding), `schemas/` + `samples/` (the oracle), `tools/`
(scripts/commands), and `flows/` (workflows) - add the pieces you need.

## Source map (`src/`)

Organized by layer (one flat `namespace Icm`; folders are for humans). `build.ps1` compiles the
`Gui\` folder only into `icm-gui.exe` and the `Cli\` folder only into `icm.exe`; everything else is
shared.

```
src/
  Conventions.cs   the instance contract in one place: file/dir layout + intent/tool/node kind names
  Json.cs          serde_json analogue: JavaScriptSerializer wrapper + navigation + Obj/Schema builders
  Model/           pure data (no logic)
    Config.cs        icm.config.json (model seats, tools, compat shim) + Tool (command/script helpers)
    Manifest.cs      the routing index + Entry
    TableSchema.cs   ColSpec / TableSchema / Problem (the oracle's data)
    Flow.cs          Flow / FlowNode (the workflow data)
    Results.cs       TurnResult / ValidateResult / ProposeResult / ToolRunResult
  Runtime/         the engine
    Instance.cs      a loaded ICM + sandboxed IO (path-escape guard) + path helpers + IcmError
    Oracle.cs        the schema-driven TSV validator (the D2 "compiler")
    Tsv.cs           shared CRLF-tolerant line/row splitting
    ToolRunner.cs    runs declared command/script tools (argv quoting, stdin, timeout, capture)
    Ollama.cs        the Ollama client + Cancel handle (see the deviation note below)
    Dispatcher.cs    conversation rewrite -> constrained classify -> capability (ask/make/validate/propose)
    FlowEngine.cs    runs an authored Flow over a state blackboard
  Server/  Mcp.cs   stdio JSON-RPC: tools/list + tools/call dispatched by kind
  Cli/             the console exe (Main)
    Program.cs       command parsing
    ConsoleChat.cs   the REPL over a Dispatcher
    SelfTest.cs      `icm selftest` - asserts the deterministic core
  Gui/             the WinForms exe (Main) - WinForms-only code
    Gui.cs           the "VSCode lite" front end (window, tree, editor, chat panel, dialogs, theme)
    Native.cs        Win32 interop: explorer tree glyphs, system icons, modern folder picker
```

Launchers `run-cli.ps1` / `run-gui.ps1` + `icm.cmd` / `icm-gui.cmd` run the exes in-memory (see the
Smart App Control section).

## The one deliberate deviation from the Rust version

The Rust client hand-rolls HTTP over a raw `TcpStream` (manual request, read-to-EOF, manual
de-chunk) specifically to avoid pulling in an HTTP crate. This port uses `System.Net.HttpWebRequest`
instead. That is the same spirit ("use the standard library, no extra dependency"): `HttpWebRequest`
lives in `System.dll`, which is always present. It also handles `Transfer-Encoding: chunked` and
response framing for us, removing the manual de-chunk loop. The DEVLOG read-timeout lesson is
preserved via `Timeout` + `ReadWriteTimeout`, and the grammar-constrained `format` field is
preserved as-is.

## Parity and skeleton boundaries

Behaviour matches the Rust host: `open`, oracle PASS/FAIL, live `gen`, live `chat` dispatcher,
and the MCP handshake + `tools/list` are all verified. The same skeleton edges remain (they are
ported faithfully, not finished here): `tools/call` reports "not implemented yet"; cross-table
ref checks exist (`Oracle.IdSet` + the refs map) but `validate` runs single-table and passes
`null`; the config-compat shim for flat Python `model`/`embed_model` fields is not added; output
prints after completion (no token streaming).
