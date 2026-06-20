<!--icm
{
  "id": "manage-a-project",
  "title": "Create and manage a multi-file project",
  "doc_type": "recipe",
  "group": "automation",
  "summary": "The project lifecycle in this instance: scaffold with /new, add files with /add (generate -> whole-project build oracle -> repair -> register), build with /build, run with /run, and inspect state with /project. Memory lives on disk (project.json + PROJECT.md), not in the model's context.",
  "keywords": ["project", "new", "add", "build", "run", "project status", "lifecycle", "manage", "multi-file", "scaffold", "project.json", "PROJECT.md", "memory", "add_file", "register"],
  "source": { "origin": "authored", "repo_path": "win_icm_code/windows-icm/flows/add_file.json", "note": "the project tools + add_file flow; deterministic chain verified end-to-end" }
}
-->
# Create and manage a multi-file project

A project here is a folder under `out\<name>` laid out per [[project-layout]] and stamped from
[[project-structured]]. The lifecycle is a small set of instance commands; the host stays a generic
harness (see the host/instance boundary). Correctness comes from the **whole-project compile**, and
project state lives **on disk** so it does not depend on the model's context window.

## The lifecycle

1. **Create** - `/new <name> [console|winforms]`
   Scaffolds `src\Core` + `src\Drivers` (or `src\Ui` for winforms), `response.rsp`, `build.ps1`,
   `project.json` (machine memory) and `PROJECT.md` (narrative memory). Refuses to overwrite. The
   fresh project already builds.

2. **Add a file** - `/add <name> <description-or-path>`
   Runs the `add_file` flow: it grounds the model in the KB **and** the project's own code, chooses a
   target path (or honors one you typed), generates the file, builds the **whole project** as the
   oracle, repairs until it compiles, then registers the file in `project.json` + `PROJECT.md`.
   - `/add App a TCP output driver` - the flow proposes `src\Drivers\<Name>.cs`.
   - `/add App src\Core\Parser.cs a recursive-descent parser` - the flow honors the explicit path.

3. **Build** - `/build <name>`
   `csc -noconfig @response.rsp` over the whole tree (`/recurse:src\*.cs`), to `dist\<name>.exe`.
   The build script never changes as files are added.

4. **Run** - `/run out\<name>\dist\<name>.exe`
   Launches the built exe with the in-memory loader (SAC-safe; your own code on your own machine).

5. **Inspect** - `/project <name>`
   Prints the project's memory: `PROJECT.md`, `project.json`, the `src` tree with each file's role,
   any manifest drift (files on disk with no recorded role, or recorded files now missing), and an
   adapter template. This is also the Tier-0 context the `add_file` flow reads.

## Why the project build is the oracle

The single-file loop ([[generate-compile-repair]]) asks "does this snippet compile?" A project asks
"does the whole thing still compile?" `add_file` writes the candidate into `src\` and rebuilds
everything, so a new file that misuses a sibling's interface fails and is repaired. The model never
has to hold the project in its head - the compiler is the ground truth and the on-disk memory is the
record.

## "Compiles" is not "works" - wire new code into the entry point

The oracle here is the **whole-project compile**, not behavior. `/add`-ing a `Core` class makes the
project still build, but the app does not *use* that class until something calls it. To get a runnable
app, regenerate the entry point so it wires the new code in:

- `/add <name> src\Program.cs <describe what the app should DO when run, using the existing types>`

Targeting `src\Program.cs` is the one case where the generated file must contain `Main` - the flow
detects this and requires a single `Main` that calls your `Core`/`Drivers` types (every other target
forbids `Main`). A typical session is therefore: scaffold, add the pieces, then add/replace
`Program.cs` to tie them together and run.

## Where to put things
- Pure domain logic (no I/O, no UI) -> `src\Core`.
- An implementation of an interface/seam (file, network, etc.) -> `src\Drivers`, copying an existing
  sibling as a template.
- A WinForms window -> `src\Ui`.
