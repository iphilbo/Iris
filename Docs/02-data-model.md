# Data Model

## Investor

Represents one investor row in the UI.

### Fields

- `id` (string; GUID or stable ID)
- `name` (string)
- `mainContact` (string)
- `contactEmail` (string)
- `contactPhone` (string)
- `category` (string; e.g. `"existing" | "known" | "new"`)
- `stage` (string; e.g. `"target" | "contacted" | "NDA" | "due_diligence" | "soft_commit" | "commit" | "closed" | "dead"`)
- `commitAmount` (number; e.g. decimal stored as number)
- `notes` (string)
- `tasks` (array of `Task`)
- Audit-ish fields (optional but recommended):
  - `createdBy` (string; userId)
  - `createdAt` (ISO datetime string)
  - `updatedBy` (string; userId)
  - `updatedAt` (ISO datetime string)

## Task

Represents per-investor to-dos.

### Fields

- `id` (string; GUID or stable ID)
- `investorId` (string; FK to investor)
- `description` (string) – the task description
- `dueDate` (string; `YYYY-MM-DD`)
- `done` (boolean)
- Optional:
  - `createdAt`, `updatedAt` (ISO datetime strings)

Tasks are nested inside the investor in JSON, but the `investorId` is still stored for clarity.
