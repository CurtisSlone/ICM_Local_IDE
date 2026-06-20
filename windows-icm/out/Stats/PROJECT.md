# Stats

## Purpose
One or two lines: what this is and why it exists.

## Architecture
Single exe via /recurse. Core is pure; Drivers holds IOutput implementations. Program composes them.

## Status / TODO
- [x] scaffold + first green build
- [ ] (next feature)

## Changelog
- 2026-06-19  scaffolded (console)
- 2026-06-19  added src/Core/Statistics.cs (domain: mean/min/max over doubles)
- 2026-06-19  added src/Program.cs (entry point: parse args, print stats via Core.Statistics)
