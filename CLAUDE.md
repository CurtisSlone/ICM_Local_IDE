# CLAUDE.md: win_icm_code (the C# ICM host)

Orientation for working in this directory. Read this first, then `README.md` for the user-facing
details. This is a from-scratch C# reimplementation of the Rust ICM host that lives one level up at
`../ollama_ICM_code`; the Python originals are in `../ICM-Local-Model-*`.

## What this is

A host that opens an instance directory and runs it, two Windows-native executables over one shared
codebase:

- `icm.exe` - the console CLI (open / chat / mcp / flow / validate / gen / selftest).
- `icm-gui.exe` - a "VSCode lite" WinForms front end (file tree + editor + chat panel).

An **instance** (e.g. `example-icm/`) is pure data the host loads: a KB, table schemas, scripts
(tools), and authored workflows (flows). The host is domain-agnostic; all domain content lives in
the instance.

**ICM = Interpretable Context Methodology** ("Folder Structure as Agent Architecture"), by Jake Van
Clief & David McDermott (University of Edinburgh / Eduba; MIT; arXiv:2603.16021; repo
RinDig/Interpretable-Context-Methodology-ICM). ICM replaces framework-level multi-agent orchestration
with filesystem structure: folders are stages, plain markdown carries the prompts/context for one
orchestrating agent, local scripts do the non-AI work, and every stage output is a plain file a human
can edit before the next stage runs. Do NOT describe ICM as "a proposer plus an oracle" - that is
THIS project's adaptation, not ICM itself. This project applies ICM to a LOCAL model and ADDS a
deterministic oracle as the reliability mechanism (the local model proposes; the oracle decides).
Spelling is "Interpretable" (per the paper), not "Interpreted".

## The core thesis (do not lose this)

The model PROPOSES; a deterministic ORACLE decides. Reliability comes from structure plus the
oracle, not from the model being smart. Three roles, one trust line:

- EMBEDDER narrows candidates; never decides or generates. (Not yet implemented in this host.)
- BASE MODEL proposes: picks from an enum, drafts, writes a row. Never trusted to be right alone or
  to pick the next step.
- ORACLE (here: a JSON-schema-driven TSV validator) accepts or rejects. It has no opinions.

The guardrails that keep a weak local model reliable:
- **Dispatcher, not chat.** Each chat turn is ONE constrained classify call (`{intent, query}`),
  then code runs the chosen capability. Never an open tool-calling loop.
- **Tools are declared, args are filled.** A tool's command is authored in the instance config; the
  model only fills constrained arguments. Tool SELECTION is the strong orchestrator's (Claude over
  MCP) or an authored flow's, never an open local-model loop.
- **The host owns the oracle.** Deterministic validation lives in C#, not in the model or scripts.

## Build and run

No .NET SDK on this machine - we build with the **in-box .NET Framework `csc.exe`** (pre-Roslyn,
C# 5). No NuGet, no MSBuild.

```
powershell -ExecutionPolicy Bypass -File build.ps1     # writes icm.exe and icm-gui.exe
```

`build.ps1` globs `src\` recursively and partitions by folder: `Cli\` (console Main) is excluded
from the GUI build, `Gui\` (WinForms + GUI Main) from the console build.

**Smart App Control blocks running the unsigned exes directly.** Run them in-memory via the
launchers, which load the assembly bytes inside trusted `powershell.exe`:

```
.\icm.cmd validate example-icm tasks
.\icm-gui.cmd example-icm
```

Do NOT run `.\icm.exe` / `.\icm-gui.exe` directly (SAC will block them). The prebuilt exes are
committed so downloaders can use the launchers immediately.

Verify the deterministic core with no model: `.\icm.cmd selftest` (asserts the oracle, JSON, TSV,
argv quoting, the path-escape guard, and path conventions).

## Project structure (`src/`)

One flat `namespace Icm`; folders are organizational only.

- `Conventions.cs` - the instance contract in one place: file/dir layout + intent/tool/node kind
  constants. Reach for these instead of hardcoding strings.
- `Json.cs` - JavaScriptSerializer wrapper + navigation + `Obj`/`Schema`/`EnumProp`/`StrProp` builders.
- `Model/` - pure data: Config, Manifest, TableSchema, Flow, Results.
- `Runtime/` - the engine: Instance (sandboxed IO + path-escape guard), Oracle, Tsv, ToolRunner,
  Ollama, Dispatcher, FlowEngine.
- `Server/Mcp.cs` - stdio JSON-RPC (`tools/list` + `tools/call`).
- `Cli/` - the console exe: Program, ConsoleChat, SelfTest.
- `Gui/` - the WinForms exe (WinForms-only code): Gui.cs, Native.cs.

## Commands

```
icm open <dir>                 load + summarize an instance
icm chat <dir>                 dispatcher console (needs Ollama)
icm mcp  <dir>                 serve the instance over MCP (stdio) - Claude connects here
icm flow <dir> <name> [in...]  run an authored workflow (flows/<name>.json)
icm validate <dir> <table>     run the oracle on schemas/<table>.json + samples/<table>.txt
icm gen  <dir> <prompt...>     one raw generate call
icm selftest                   check the deterministic core (no model)
```

`OLLAMA_URL` overrides the config `ollama_url`. Ollama is local plaintext on `localhost:11434`.

## The instance contract

An instance directory may provide (all optional except as noted; see `example-icm/`):

- `icm.config.json` - `name`, `domain`, `models {generate, dispatch, embed}` (flat `model` /
  `embed_model` also accepted for the Python ICMs), `ollama_url`, `tools [...]`. Missing config is
  tolerated (defaults + KB only).
- `manifest.json` - `entries [{id, title, path, summary}]` (the routing index; summaries are all
  routing sees).
- `kb/*.md` - one topic per file; the grounding the model reads.
- `SYSTEM.md` - operating rules injected into the answer prompt.
- `schemas/<table>.json` + `samples/<table>.txt` - the oracle's schema and TSV data.
- `tools/*` - scripts the host runs (declared in config with a `command` argv or a `script`).
- `flows/*.json` - authored workflows.

## Changing configuration

Config is **per-instance** in `<instance>/icm.config.json`; there is no global host config. The
fields that matter, and how to change them:

- **Model(s).** `models.generate` (writes text), `models.dispatch` (the classify/route call; falls
  back to `generate` if omitted), `models.embed` (optional). Set them to any model present in Ollama
  (`ollama list` / `ollama pull <name>`). Flat `model` / `embed_model` / `dispatch_model` fields are
  accepted as fallbacks. Resolution: `Config.ResolveModels` in `src/Model/Config.cs` (nested wins,
  then flat, then defaults `qwen3-coder:latest` / none).
- **Ollama connection.** `ollama_url` (default `http://localhost:11434`). The `OLLAMA_URL` env var
  overrides the file at runtime - env wins. Resolution: `EffectiveUrl` in `src/Cli/Program.cs` (and
  the GUI reads the env the same way).
- **Other.** `name`, `domain` (shown by `open`, woven into prompts), `tools [...]` (see below).

To change a model or endpoint for an instance, edit that instance's `icm.config.json` and re-run.
Verify with `icm open <dir>` - it prints the resolved `generate` / `dispatch` / `embed` seats and the
effective Ollama URL. No host rebuild is needed for config changes (config is data, read at load).

## Adding capabilities

- A new **tool**: add an entry to the instance's `icm.config.json` (`command`/`script` + optional
  `inputSchema`/`stdin`/`timeout`/`env`). No host change needed.
- A new **flow node kind**: add a `case` in `Runtime/FlowEngine.cs` and a constant in
  `Conventions.Node`.
- A new **dispatcher intent / tool kind**: add the constant in `Conventions`, a branch in
  `Dispatcher.Turn` / `Mcp.CallTool`. (If these multiply, consider a capability registry instead of
  the switches - deferred for now.)
- Always extend `Cli/SelfTest.cs` when you add deterministic logic worth guarding.

## Style

Plain and grounded: no hype, no emoji, no em dashes (matches the parent project). C# 5 only (the
in-box compiler caps there): no string interpolation, no expression-bodied members, no
auto-property initializers in getters-only form. Single `namespace Icm`. One public type per file
where reasonable; `Gui.cs` is intentionally still one file (a future pass may split it into
ChatPanel / WorkspaceTree / EditorView).
