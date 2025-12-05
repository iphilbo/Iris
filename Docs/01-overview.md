# Iris - RaiseTracker Feature – Overview

This document describes the design for the **RaiseTracker** feature within the **Iris** project:
- File-based storage using many JSON files + an index
- Simple 7-user password authentication
- 7-day sliding session cookies
- Minimal REST API to support the HTML front-end

Intended to be used as a reference/spec in Cursor while you implement.

## System Overview

- Small internal web app (7 users).
- Purpose: Track investors and related tasks for Series A.
- Project: **Iris** - Feature: **RaiseTracker**
- Front-end: Single HTML/JS page (existing "vibe-coded" page, refactored).
- Backend: Thin HTTP API (e.g., .NET 8 minimal API, but framework-agnostic).
- Storage: Azure Blob Storage with JSON files (no database).
- Auth:
  - User picks their name and enters a per-user password.
  - Passwords stored only on server as hashes.
  - On success, a session cookie is issued with 7-day sliding expiry.
- Hosting: Azure App Service (HTTPs only).
