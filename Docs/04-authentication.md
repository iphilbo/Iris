# Authentication and Sessions

## Login UX

User visits `/`.

If no valid session cookie:

Show a login screen with a list of user display names loaded from the backend:

e.g., "Alice", "Bob", "Carol".

User clicks their name, sees a password field, enters their password, and submits.

## Login Request

Frontend sends:

- `userId` (or username)
- `password` (plaintext, over HTTPS)

to `POST /api/login`.

## Login Handling (Backend)

Backend steps:

1. Load `users.json` and find matching user by `userId` or `username`.
2. Hash the supplied password with the same algorithm and parameters used to create `passwordHash`.
3. Compare computed hash with stored `passwordHash`.

If match:

- Create a session object:
  - `userId`
  - `issuedAt` (UTC)
  - `expiresAt` = issuedAt + 7 days
- Serialize and sign it (e.g., JWT or opaque token with HMAC).
- Set it as an HTTP-only, Secure cookie (e.g., `AuthSession`).

If not match:

- Return HTTP 401/403 with generic error message ("Invalid credentials").

## Session Validation and Sliding Expiry

On each authenticated request:

1. Check for the `AuthSession` cookie.
2. Validate the signature and parse the payload.

If invalid or expired:

- Treat request as unauthenticated (401).

If valid:

- Attach `userId` (and maybe `displayName`, `isAdmin`) to the request context.
- Apply sliding expiry:
  - If `expiresAt - now` is below some threshold (e.g., < 2 days), issue a new cookie with `now + 7 days`.

Result: As long as users are actively using the app, their session keeps extending. If they stop using it or clear cookies, they log in again.

## Authorization

Baseline for now:

- All authenticated users have full read/write access to investors and tasks.
- `isAdmin` is present in `users.json` for future use (e.g., user management, bulk operations).

Later, if needed:

- Restrict certain endpoints (e.g., user management) to `isAdmin == true`.
- Add owner semantics to investors.
