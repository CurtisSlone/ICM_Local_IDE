# TodoList

## Purpose
One or two lines: what this is and why it exists.

## Architecture
Single exe via /recurse. Pure logic in src/Core, I/O adapters in src/Drivers, wired together in Program.cs.

## Status / TODO
- [x] scaffold + first green build
- [ ] (next feature)

## Changelog
- 2026-06-19  scaffolded (console)
- 2026-06-19  added src/Core/TodoItem.cs (TodoItem represents core task entities with id, title, and completion status.)
- 2026-06-19  added src/Drivers/ITodoStore.cs (Defines the contract for todo item storage operations.)
- 2026-06-19  added src/Drivers/FileTodoStore.cs (File-based todo store persisting to todos.txt with CRUD operations.)
- 2026-06-19  added src/Program.cs (CLI todo app entry point using file-based storage.)
