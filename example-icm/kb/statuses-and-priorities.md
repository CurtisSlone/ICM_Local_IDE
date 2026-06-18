# Statuses and priorities

## Status

The `status` column records where a task sits in its lifecycle. It must be exactly one of:

- `todo` — not started yet.
- `doing` — in progress right now.
- `done` — finished.

Any other value (e.g. "in progress", "complete", "WIP") is rejected by the oracle. Use the exact
spellings above.

## Priority

The `priority` column is an integer on a 1-to-5 scale, where **1 is the highest priority** and **5
is the lowest**:

- `1` — urgent / do first
- `2` — high
- `3` — normal
- `4` — low
- `5` — someday / nice to have

Values outside 1-5 are rejected. Leave it blank only if the table's schema marks it optional (this
one does).
