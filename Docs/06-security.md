# Rate Limiting and Safety

Even with 7 users, add minimal protections:

## Login Rate Limiting

Per `userId` and/or per IP:

- Limit login attempts:
  - e.g., max 5 failed attempts in 15 minutes.
- On lockout:
  - Return 429 or 403 with a generic message.
  - Optionally log that the user is temporarily locked.

Implementation can be:

- Simple in-memory dictionary keyed by `userId + IP` with timestamps and counters.
- (If scaling later, migrate to Redis or similar.)

## Logging

Log:

- Successful logins (`userId`, timestamp, IP).
- Failed logins (`userId`, timestamp, IP) – without logging the password.
- Create/update/delete of investors and tasks (who did what, when).

## HTTPS Only

- Enforce HTTPS in App Service configuration and/or middleware.
- Set cookie with `Secure` and `HttpOnly` flags.

## Blob Access and Concurrency

Use Azure Blob Storage with:

- Container: `seriesa-data`
- Files:
  - `index.json`
  - `investors/{id}.json`
  - `users.json` (server-only; not read from client).

### Permissions

- Storage account: no public/container access.
- Backend uses managed identity or connection string to access blobs.
- Front-end never talks directly to blob; only via the backend API.

### Concurrency (ETags)

For `index.json` and each `investors/{id}.json`:

- On read, capture the blob's ETag.
- On write, include ETag in the conditional write:
  - "If-Match" ETag.
- If blob changed since read:
  - Write fails with a precondition error.
  - Backend returns a conflict to the client.
  - Front-end can prompt "Data changed, please reload."

This keeps you from silently stomping on someone else's changes, even if rare.
