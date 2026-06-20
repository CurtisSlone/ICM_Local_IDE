# Expr

## Purpose
One or two lines: what this is and why it exists.

## Architecture
Single exe via /recurse. Pure logic in src/Core, I/O adapters in src/Drivers, wired together in Program.cs.

## Status / TODO
- [x] scaffold + first green build
- [ ] (next feature)

## Changelog
- 2026-06-19  scaffolded (console)
- 2026-06-19  added src/Core/Tokenizer.cs (Tokenizer splits arithmetic expressions into numbers and operators.)
- 2026-06-19  added src/Core/Evaluator.cs (Evaluates arithmetic expressions with proper operator precedence and parentheses handling.)
- 2026-06-19  added src/Program.cs (src\Program.cs evaluates command-line expressions and handles errors.)
