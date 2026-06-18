# Extending this ICM

This folder is a template. Copy it and edit the pieces you need - the host runs whatever it finds.

- **Knowledge base** — add a `kb/<topic>.md` file and an entry in `manifest.json` (`id`, `title`,
  `path`, and a sharp one-line `summary`). The summary is the only thing routing sees, so make it
  discriminating. Grounded `ask` answers come from these files.
- **Oracle** — add `schemas/<table>.json` (columns with `type`, `required`, `min`/`max`, `values`
  for enums) and `samples/<table>.txt` (a tab-separated file; the first line is the header). The
  oracle then validates that table and gates `propose`.
- **Tools** — declare a tool in `icm.config.json` with a `command` (argv array) or a `script`
  (`.ps1` under `tools/`). The host runs it with this folder as the working directory and fills
  `{arg}` placeholders from the call. The model never invents the command - it only fills declared
  arguments.
- **Flows** — add `flows/<name>.json`: an ordered list of nodes (`route`, `read`, `generate`,
  `answer`, `propose`, `validate`, `tool`) that compose the model, the oracle, and tools into one
  authored workflow. Run it with `icm flow <dir> <name>` or expose it as a `kind:"flow"` tool.

The rule that keeps a local model reliable: the model only ever PROPOSES inside a constrained step;
code, the oracle, and authored flows decide what happens. See the repo's CLAUDE.md and README.
