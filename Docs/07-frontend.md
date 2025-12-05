# Front-end Behavior

## On Load

- Check if the session cookie is present and valid (via an `/api/session` check or by attempting `GET /api/investors`).
- If unauthorized → show login screen.
- If authorized → load `GET /api/investors` and render the table.

## Login

- Call `GET /api/users` to list names.
- On name selection + password entry, call `POST /api/login`.
- On success → reload main app state.

## Investor Actions

- Add/edit investor via a form at top.
- Task management via per-investor sections (under each row).

## Error Handling

Periodically:

- Handle 401 errors centrally: if any API call returns 401, redirect back to login.
