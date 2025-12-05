# Implementation Notes for Cursor

When you move this into Cursor:

## Implementation Order

Start by implementing:

1. `users.json` reading/writing and the login flow (hashing + cookie).
2. Then the investor index + per-investor file handling (without worrying about ETags initially).
3. Add ETag-based concurrency once basic CRUD works.

## Using This Documentation

Use this document as context so Cursor understands:

- Auth model
- Storage layout
- Expected JSON shapes
- Session behavior (7-day sliding expiry)

You can paste sections of this doc into Cursor prompts as needed ("Implement the `/api/login` endpoint following this spec," etc.).
