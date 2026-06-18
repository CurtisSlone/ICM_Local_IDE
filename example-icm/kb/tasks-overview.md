# Tasks overview

A task is one row in the `tasks` table (`samples/tasks.txt`), a tab-separated file whose first line
is the header. Each task has four columns, in this order:

- `id` — a unique integer, 1 or greater. It is the table's key (other tables could reference it).
- `title` — a short free-text description of the work. Required.
- `status` — where the task is in its lifecycle. One of a fixed set (see "Statuses and priorities").
- `priority` — how urgent the task is, on a small numeric scale (see "Statuses and priorities").

The oracle (`schemas/tasks.json`) enforces this shape: every row must have exactly four
tab-separated columns, `id` must be an integer, `title` must be present, `status` must be one of the
allowed values, and `priority` must sit in range. A row that breaks any rule is rejected.

A well-formed row looks like:

```
7	Write the release notes	doing	2
```
