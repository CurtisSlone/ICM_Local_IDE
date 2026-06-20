# EditDemo

## Purpose
One or two lines: what this is and why it exists.

## Architecture
Single exe via /recurse. Pure logic in src/Core, I/O adapters in src/Drivers, wired together in Program.cs.

## Status / TODO
- [x] scaffold + first green build
- [ ] (next feature)

## Changelog
- 2026-06-19  scaffolded (console)
- 2026-06-19  added src/Core/Stats.cs (Provides static average calculation for integer arrays with empty input validation.)
- 2026-06-19  edited src/Core/Stats.cs (Rewrote Average method to use foreach loop instead of for loop.)
- 2026-06-19  edited src/Core/Stats.cs (Changed null check to throw ArgumentNullException with parameter name "xs".)
