# How this instance is organized

A growing library of Windows / C# / PowerShell know-how, organized by purpose (the layout mirrors a
"companion" repo: references, patterns, recipes, scaffolds, snippets). The host routes over it via
`manifest.json`.

```
windows-icm/
  manifest.json   the routing index - one entry per file you want routable (id, title, path, summary)
  SYSTEM.md       operating rules injected into grounded answers
  reference/      raw reference: language/tool facts, environment constraints, API notes
  patterns/       complete reusable units (a tool-script pattern, a WinForms form, an arg parser, ...)
  recipes/        step-by-step playbooks that chain patterns ("build a deploy script end-to-end")
  scaffold/       project/file scaffolds to copy and fill
  snippets/       small fragments (a one-liner, a regex, a P/Invoke signature)
  tools/          scripts the host can run (csc_check.ps1, ps_parse.ps1, ...)
  flows/          authored workflows (write_csharp, write_powershell, answer)
```

## Adding a reference (so the model can use it)

1. Drop a markdown file in the right folder, e.g. `patterns/winforms-form.md`.
2. Add an entry to `manifest.json` pointing at it, with a **sharp, discriminating one-line
   `summary`** - the summary is the only thing routing sees, so make it specific.
3. That's it - `ask` and the flows can now route to it. (Files without a manifest entry are still
   readable by tools/flows by path, just not auto-routed.)

Keep each file to one topic. Lead with the facts the model would otherwise get wrong (this
environment is C# 5 / PowerShell 5.1, not modern .NET / pwsh 7).
