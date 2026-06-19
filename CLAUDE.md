# CLAUDE.md: win_icm_code (the C# ICM host)

Orientation for working in this directory. Read this first, then `README.md` for the user-facing
details. This is a from-scratch C# reimplementation of the Rust ICM host that lives one level up at
`../ollama_ICM_code`; the Python originals are in `../ICM-Local-Model-*`.

## What this is

A host that opens an instance directory and runs it. The primary interface is a **terminal operator
console**: `icm <dir>` opens an ICM and drops you into a chat REPL where you plan in natural language
and act with slash commands (built to run inside a VSCode integrated terminal, like `code <dir>`).
Two Windows-native executables over one shared codebase:

- `icm.exe` - the console CLI and operator console (`icm <dir>` / open / chat / mcp / flow / list /
  validate / gen / selftest). This is the main single-operator interface.
- `icm-gui.exe` - an OPTIONAL "VSCode lite" WinForms desktop front end (file tree + editor + chat
  panel). Secondary; the terminal console is the primary path. It is not an IDE - the project is the
  ICM host and its interface is the operator console.

An **instance** (e.g. the bundled `windows-icm/`) is pure data the host loads: a KB, table schemas,
scripts (tools), and authored workflows (flows). The host is domain-agnostic; all domain content
lives in the instance.

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
- **Operator drives; the model never picks actions.** Chat turns split two ways (`Dispatcher.Turn`):
  a line starting with `/` is an explicit slash command dispatched deterministically to a capability
  or flow (`/ask`, `/write`, `/ps`, `/make`, `/list`, `/search`, `/validate`, `/propose`, `/flow`,
  `/chat`, `/flows`, `/note`, `/notes`, `/do`, `/clear`, `/help`, `/quit`). Plain text runs through the
  conversational **router** (`RouteConversational`): the model proposes a flow from the closed catalog
  (grammar-constrained enum), a deterministic gate (`Dispatcher.Gate`) accepts only an on-list,
  non-low-confidence match, and it runs after a y/n confirm or falls back to `/ask` - mode set by
  `router.autorun` (confirm | on | off, default confirm). Unrecognized commands fall back to `/ask`.
  `/chat` is free conversation; `/do` is the opt-in classify-and-route path (`{intent, query}`). Never
  an open tool-calling loop. A command's output
  can be redirected to a workspace file with a trailing `> path` (markdown fences stripped, so a
  `.cs`/`.ps1` lands clean and the GUI opens it); writes and `/note` lines persist in `NOTES.md`,
  which the chat reads back as session context.
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
.\icm.cmd windows-icm                       # VSCode-style: open the operator console on a dir (rel or abs)
.\icm.cmd open windows-icm
.\icm.cmd flow windows-icm csharp "a method that reverses a string"
.\icm-gui.cmd windows-icm
```

`icm <dir>` is the primary terminal entry point: it opens the chat console on that ICM directory.
The path is resolved against the terminal's working directory (the launcher forwards argv unchanged
through the in-memory load - SAC only changes HOW the bytes load, not the arguments). Put the
`win_icm_code` dir on PATH and you can run `icm .` or `icm ..\myproj` from any folder, including the
VSCode integrated terminal:

```
setx PATH "%PATH%;C:\Users\curti\Documents\ollama_ICM_Code\win_icm_code"   # once, then reopen the terminal
```

Do NOT run `.\icm.exe` / `.\icm-gui.exe` directly (SAC will block them). The prebuilt exes are
committed so downloaders can use the launchers immediately. The WinForms GUI (`icm-gui.cmd`) still
works but is secondary now - the terminal console is the main single-operator interface.

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
icm docsearch <dir> <corpus> [-k N] [--no-embed] <query...>   hybrid search a refdocs corpus
icm reindex <dir>              regenerate manifest.json from files' <!--icm--> metadata blocks
icm list  <dir> [--group G] [--type T] [--json]   enumerate the KB catalog
icm gen  <dir> <prompt...>     one raw generate call
icm selftest                   check the deterministic core (no model)
```

`OLLAMA_URL` overrides the config `ollama_url`. Ollama is local plaintext on `localhost:11434`.

## The instance contract

An instance directory may provide (all optional except as noted; see `windows-icm/`):

- `icm.config.json` - `name`, `domain`, `models {generate, dispatch, embed}` (flat `model` /
  `embed_model` also accepted for the Python ICMs), `ollama_url`, `tools [...]`. Missing config is
  tolerated (defaults + KB only).
- `manifest.json` - the routing index: `entries [{id, title, path, summary, doc_type, keywords}]`.
  The dispatcher routes on `summary` + `keywords`; grounding then reads the file. **This file is
  generated** - do not hand-edit it. Author the routing metadata in each source file's `<!--icm-->`
  block (below) and run `icm reindex <dir>` to regenerate it mechanically (no LLM summarization).
- Routable reference files live under the routable folders (`Conventions.RoutableDirs`: `reference`,
  `patterns`, `recipes`, `scaffold`, `snippets`, `kb`) and lead with a metadata block in an HTML
  comment (invisible in rendered markdown, parsed by the indexer):
  ```
  <!--icm
  { "id": "...", "title": "...", "doc_type": "reference",
    "summary": "one sharp line - the only thing routing sees besides keywords",
    "keywords": ["..."], "source": { "origin": "...", "url": "...", "note": "..." } }
  -->
  ```
  `source` carries provenance for cite-and-verify (the knowledge-oracle pattern); it is not routed
  on. `icm reindex` skips files with no block (and `README.md` folder guides). Grounding reads
  (`Instance.ReadEntry`) strip the block so the model sees clean content.
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

Prefer **composing primitives in an authored flow** over adding host code. The host should stay
small, general primitives; capabilities belong in instance content (flows/skills). The flow node
kinds are the primitives: `route`, `read` (one id OR a comma/newline list, each read with metadata
stripped + a header), `generate`, `answer`, `propose`, `validate`, `tool`,
`loop` (repeat a `body` until a state key is truthy, or `maxIterations` times - the bounded
repair/retry primitive), `branch` (run a `then` or `else` body based on a `when`/`test` on a state
key: `empty`/`nonempty`/`truthy`/`falsy` - the conditional primitive), `search` (hybrid
BM25+embedding search over a corpus - `refdocs/<corpus>.json` if present, else the shipped
`refdocs-seed/<corpus>.json`; vectors cache in `refdocs/` - writing the hits to `context`), `route_many`
(constrained MULTI-pick of up to `maxK` relevant manifest ids - the model proposes a SET, the host
reads them all), and `catalog` (write the manifest index, optionally filtered by `group`/`doc_type`,
to a state key for the model to enumerate). The bundled `windows-icm/flows/answer_fallback.json`
composes these into tier-2 grounding: route+read the KB, then `branch` when `context` is empty to
`search` the docs corpus, then `answer`; `windows-icm/flows/write_grounded.json` uses `route_many` to
ground generation on every relevant pattern before the compile-repair `loop`. Enumeration is also
exposed for the orchestrator: `icm list` and the built-in MCP tools `catalog` + `read_entry`. For example, a
"generate code, compile, repair until it builds"
capability (a.k.a. `generate_verify`) is **not** a host feature - it is an authored `loop` flow whose
check is a declared `tool` (e.g. `csc`), feeding the tool's `{output}` back into the next `generate`
prompt. Build that as a flow in the instance, not a node in the engine.

When you genuinely do need to touch the host:
- A new **tool**: add an entry to the instance's `icm.config.json` (`command`/`script` + optional
  `inputSchema`/`stdin`/`timeout`/`env`). No host change needed.
- A new **flow node kind** (only when no composition of existing nodes works): add a branch in
  `Runtime/FlowEngine.cs` and a constant in `Conventions.Node`.
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
