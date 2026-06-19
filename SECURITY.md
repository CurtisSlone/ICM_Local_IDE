# Security & trust model

`icm` is a local host you point at an **instance** directory. Read this before running an instance you
did not write.

## The one rule: only open instances you trust

An instance's `icm.config.json` can declare **command/script tools** - PowerShell scripts and external
programs the host will run (with the instance folder as the working directory). Opening such an
instance and using a capability that invokes a tool **runs that code on your machine**. This is by
design (it is how the host does real work), but it means:

> Treat opening an instance like running its scripts, because that is what it is. Only open instances
> from a source you trust, and skim `icm.config.json` + the `tools/` folder first.

This applies equally when a **frontier model drives the host over MCP**: the model can call the
declared tools, so the instance's tools define the blast radius - not the model. The model fills
arguments into authored commands; it cannot invent new commands.

## What limits the damage

- **The local model never picks or runs tools on its own.** It only proposes into constrained slots
  (an enum, a row, a draft); a deterministic oracle or an authored flow decides what runs. There is no
  open tool-calling loop for the local model.
- **Tools are declared, not invented.** A tool's command line is authored in `icm.config.json`; the
  model only fills declared arguments. Arguments are passed as an argv (not a shell string), so there
  is no shell-injection surface from argument values.
- **File I/O is sandboxed to the instance root.** Reads/writes resolve through a path-escape guard
  that rejects absolute paths and `..`, so the host's own file operations cannot wander outside the
  opened folder. (Note: this guards the *host's* I/O. A declared external tool is a separate process
  and is bounded only by OS permissions - see the trust rule above.)
- **No network egress except Ollama.** The host talks only to the configured `ollama_url`
  (default `http://localhost:11434`, local plaintext HTTP). It does not phone home. (Instance build
  tools like `build_dotnet_prose.ps1` fetch documentation when you run them - that is the only
  outbound traffic, and only on demand.)

## Smart App Control

The committed `.exe` files are unsigned, so Smart App Control blocks running them directly. The
`.cmd` / `run-*.ps1` launchers load the program's bytes in-memory inside the Microsoft-signed
PowerShell, which SAC permits. This is your own local program running on your own machine; do not
disable SAC or change code-integrity policy to run it - use the launchers.

## Ollama

Ollama is local and unauthenticated over plain HTTP. Keep it bound to localhost. If you point
`ollama_url` at a remote host, that traffic is unencrypted - only do so on a trusted network.

## No warranty

This software is provided under the MIT License, "as is", without warranty of any kind. You are
responsible for what you run.
