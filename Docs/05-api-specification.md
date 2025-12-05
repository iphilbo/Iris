# API Surface (High Level)

## Response Formats

### Success Responses
- 200 OK: Request succeeded, body contains data
- 201 Created: Resource created (POST endpoints)
- 204 No Content: Success with no body (DELETE, some PUT)

### Error Responses
Standard error response format:
```json
{
  "error": "Error message",
  "code": "ERROR_CODE" // optional
}
```

Common status codes:
- 400 Bad Request: Invalid request data
- 401 Unauthorized: Missing or invalid session
- 403 Forbidden: Valid session but insufficient permissions
- 404 Not Found: Resource doesn't exist
- 409 Conflict: ETag mismatch (concurrency conflict)
- 429 Too Many Requests: Rate limit exceeded
- 500 Internal Server Error: Server error

## Request/Response Details

## Auth Endpoints

### GET /api/users

- Auth: none (or simple) for the login page.
- Returns: list of `[{ id, displayName }]` for login UI.
- Must not return password hashes.

### POST /api/login

- Body: `{ "userId": "user-1", "password": "…" }`
- On success: sets `AuthSession` cookie; returns 200.
- On failure: 401/403.

### GET /api/session (optional)

- Auth: requires valid `AuthSession` cookie
- Returns: `{ "userId": "user-1", "displayName": "Alice", "isAdmin": false }` or 401 if invalid
- Purpose: Check if current session is valid without loading full investor data
- Note: Frontend can use this OR simply attempt `GET /api/investors` to check auth status

### POST /api/logout (optional)

- Clears the cookie or sets it expired.

## Investor Endpoints

Require valid `AuthSession`:

### GET /api/investors

- Returns investor index (from `index.json`).
- Used to populate the main table.

### GET /api/investors/{id}

- Returns full investor (from `investors/{id}.json`).

### POST /api/investors

- Creates a new investor.
- Request body: Investor object (without `id`, `createdBy`, `createdAt`, `updatedBy`, `updatedAt` - these are set by backend)
  - Required: `name`, `category`, `stage`
  - Optional: `mainContact`, `contactEmail`, `contactPhone`, `commitAmount`, `notes`, `tasks`
- Response: 201 Created with full investor object (including generated `id` and audit fields)
- Backend:
  - Generates `id`.
  - Sets `createdBy`, `createdAt`.
  - Writes `investors/{id}.json`.
  - Updates `index.json`.

### PUT /api/investors/{id}

- Updates existing investor.
- Request body: Partial investor object (only fields to update)
- Response: 200 OK with updated investor object, or 409 Conflict if ETag mismatch
- Backend:
  - Read current file (with ETag if using Blob concurrency).
  - Apply changes.
  - Set `updatedBy`, `updatedAt`.
  - Save back and update `index.json`.

### DELETE /api/investors/{id} (optional)

- Deletes investor file and removes from index.

## Task Endpoints

Tasks live inside the investor JSON file; API endpoints conceptually:

### POST /api/investors/{id}/tasks

- Add new task to that investor.
- Request body: `{ "description": "string", "dueDate": "YYYY-MM-DD" }` (without `id`, `investorId`, `done` - `id` generated, `investorId` set from URL, `done` defaults to false)
- Response: 200 OK with updated investor object (including new task)
- Backend: Regenerates investor file with updated tasks.

### PUT /api/investors/{id}/tasks/{taskId}

- Update a task (description, dueDate, done).
- Request body: Partial task object (only fields to update)
- Response: 200 OK with updated investor object, or 409 Conflict if ETag mismatch
- Backend: Save investor file.

### DELETE /api/investors/{id}/tasks/{taskId}

- Remove the task from the tasks array.
- Response: 200 OK with updated investor object, or 404 if task not found
- Backend: Save investor file.

Internally, the server reads `investors/{id}.json`, modifies the tasks list, and writes back.
