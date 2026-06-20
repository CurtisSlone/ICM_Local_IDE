# KvStore

## Purpose
One or two lines: what this is and why it exists.

## Architecture
Single exe via /recurse. Pure logic in src/Core, I/O adapters in src/Drivers, wired together in Program.cs.

## Status / TODO
- [x] scaffold + first green build
- [ ] (next feature)

## Changelog
- 2026-06-19  scaffolded (console)
- 2026-06-19  added src/Drivers/IKvStore.cs (IKvStore defines a simple key-value store interface with get, set, delete, and keys operations.)
- 2026-06-19  added src/Drivers/InMemoryKvStore.cs (In-memory key-value store implementation using Dictionary.)
- 2026-06-19  added src/Drivers/FileKvStore.cs (File-based key-value store persisting data in kv.txt with line-oriented key=value format.)
- 2026-06-19  added src/Program.cs (Program.cs implements command-line key-value store with memory and file backends.)
