# Iris - RaiseTracker Feature – Overview

This document describes the design for the **RaiseTracker** feature within the **Iris** project:
- SQL Server database with Entity Framework Core
- Magic link email authentication
- 7-day sliding session cookies
- Minimal REST API to support the HTML front-end
- Login and pageview tracking

Intended to be used as a reference/spec for the application.

## System Overview

- Small internal web app for tracking Series A investors.
- Purpose: Track investors and related tasks for Series A fundraising.
- Project: **Iris** - Feature: **RaiseTracker**
- Front-end: Single HTML/JS page with modern UI (dark/light mode, responsive design).
- Backend: .NET 8 minimal API.
- Storage: SQL Server database (Azure SQL Database) with Entity Framework Core.
- Auth:
  - User enters their email address to receive a magic link.
  - No passwords stored - authentication via time-limited email tokens.
  - On success, a session cookie is issued with 7-day sliding expiry.
  - Login and pageview events are logged to SysLog for tracking.
- Hosting: Azure App Service (HTTPs only).
