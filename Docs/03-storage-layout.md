# Storage Layout (Azure Blob, many JSON files)

Use a single blob container, e.g. `seriesa-data`.

## Recommended Structure

- `index.json`
  - Lightweight list of investors for quick loading.
- `investors/{investorId}.json`
  - Full investor object including tasks.
- `users.json`
  - Auth users and password hashes (never exposed to clients).

## `index.json` Structure

Array of investor summaries:

```json
[
  {
    "id": "inv-123",
    "name": "Big VC Fund",
    "stage": "NDA",
    "category": "existing",
    "commitAmount": 500000,
    "updatedAt": "2025-12-03T15:30:00Z"
  }
]
```

This file is read on initial page load to build the main investor list.

## `investors/{id}.json` Structure

Full investor record:

```json
{
  "id": "inv-123",
  "name": "Big VC Fund",
  "mainContact": "Jane Smith",
  "contactEmail": "jane@example.com",
  "contactPhone": "+1-555-123-4567",
  "category": "existing",
  "stage": "NDA",
  "commitAmount": 500000,
  "notes": "Loves data-driven pitches.",
  "createdBy": "user-1",
  "createdAt": "2025-12-01T10:00:00Z",
  "updatedBy": "user-2",
  "updatedAt": "2025-12-03T15:30:00Z",
  "tasks": [
    {
      "id": "task-1",
      "investorId": "inv-123",
      "description": "Send revised deck",
      "dueDate": "2025-12-10",
      "done": false,
      "createdAt": "2025-12-03T12:00:00Z",
      "updatedAt": "2025-12-03T12:00:00Z"
    }
  ]
}
```

## `users.json` Structure

Server-only file; never served to browser.

```json
[
  {
    "id": "user-1",
    "username": "alice",
    "displayName": "Alice",
    "passwordHash": "<hash-of-password>",
    "isAdmin": true
  },
  {
    "id": "user-2",
    "username": "bob",
    "displayName": "Bob",
    "passwordHash": "<hash-of-password>",
    "isAdmin": false
  }
]
```

### Notes

- `passwordHash` should use a standard password hashing algorithm (e.g., bcrypt/PBKDF2/argon2) via whichever library the backend framework uses.
- Plaintext passwords never live in code or config; only the hashes.
