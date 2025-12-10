# Authentication and Sessions

## Login UX

User visits `/`.

If no valid session cookie:

Show a login screen with an email input field.

User enters their email address and clicks "Send Magic Link".

## Magic Link Request

Frontend sends:

- `email` (user's email address)

to `POST /api/request-magic-link`.

## Magic Link Handling (Backend)

Backend steps:

1. Look up user by email address in the database.
2. If user exists, generate a secure, time-limited token (15 minutes expiry).
3. Send magic link email via Azure Communication Services.
4. Always return success message (don't reveal if user exists for security).

## Magic Link Validation

User clicks the magic link in their email, which navigates to:

`GET /api/validate-magic-link?token={token}`

Backend steps:

1. Validate the token (check expiry and usage).
2. If valid:
   - Create a session object:
     - `userId`
     - `displayName`
     - `isAdmin`
     - `issuedAt` (UTC)
     - `expiresAt` = issuedAt + 7 days
   - Serialize and sign it using HMAC-SHA256.
   - Set it as an HTTP-only, Secure cookie (e.g., `AuthSession`).
   - Log login event to SysLog.
   - Redirect to the main application.
3. If invalid:
   - Return HTTP 400 with error message ("Invalid or expired magic link").

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
