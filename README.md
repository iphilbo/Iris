# Iris - RaiseTracker Feature

A simple web application for tracking Series A investors and related tasks. Built with .NET 8 minimal API backend and vanilla HTML/JS/CSS frontend.

**Iris** is the project name. **RaiseTracker** is a feature within Iris for tracking investors and related tasks.

## Features

- **Authentication**: Magic link email authentication with 7-day sliding session cookies
- **Investor Management**: Track investors with contact info, stage, category, status, owner, and commit amounts
- **Task Management**: Add and manage tasks per investor
- **User Management**: Admin interface for managing users and viewing login statistics
- **Login Tracking**: Automatic logging of logins and pageviews to syslog
- **Dark/Light Mode**: Toggle between themes with persistent preference
- **Mobile Responsive**: Works on desktop, tablet, and mobile devices
- **SQL Server Database**: Entity Framework Core with Azure SQL Database

## Prerequisites

- .NET 8 SDK
- Azure SQL Database (or SQL Server for local development)
- Azure Communication Services (for email/magic link functionality)

## Quick Start

**New to the project?** See [Docs/00-setup-guide.md](Docs/00-setup-guide.md) for detailed setup instructions on a new machine.

## Setup

1. **Configure Database Connection String**

   Edit `Iris.Api/appsettings.json` and update the connection string:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;"
     }
   }
   ```

2. **Configure Email Service (for Magic Links)**

   Add Azure Communication Services connection string:
   ```json
   {
     "Email": {
       "ConnectionString": "endpoint=https://YOUR_RESOURCE.communication.azure.com/;accesskey=YOUR_KEY",
       "FromEmail": "DoNotReply@yourdomain.com"
     }
   }
   ```

3. **Configure Session Signing Key**

   Update the session signing key in `appsettings.json`:
   ```json
   {
     "SessionSigningKey": "CHANGE-THIS-TO-A-RANDOM-SECRET-KEY-IN-PRODUCTION"
   }
   ```

4. **Run Database Migrations**

   The database schema will be created automatically on first run, or you can run the schema script manually:
   ```sql
   -- See Scripts/CreateDatabaseSchema.sql
   ```

5. **Run the Application**

   ```bash
   cd Iris.Api
   dotnet run
   ```

   The application will:
   - Connect to the SQL Server database
   - Start the web server (typically at http://localhost:5253)

6. **Access the Application**

   Open your browser to `http://localhost:5253`
   - Enter your email address to receive a magic link
   - Click the link in the email to log in

## Project Structure

```
Iris/
├── Docs/                    # Documentation
├── Iris.Api/                # Backend API (RaiseTracker feature)
│   ├── Models/              # Data models
│   ├── Services/            # Business logic services
│   ├── Middleware/          # Custom middleware
│   └── wwwroot/             # Frontend files
│       ├── index.html
│       ├── css/
│       └── js/
└── README.md
```

## API Endpoints

### Auth
- `GET /api/users` - Get list of users (admin only for full details)
- `POST /api/request-magic-link` - Request magic link email
- `GET /api/validate-magic-link` - Validate magic link token and create session
- `GET /api/session` - Check current session
- `POST /api/logout` - Logout
- `POST /api/pageview` - Log pageview (called automatically on page load)

### Investors
- `GET /api/investors` - Get investor index
- `GET /api/investors/{id}` - Get full investor details
- `POST /api/investors` - Create new investor
- `PUT /api/investors/{id}` - Update investor
- `DELETE /api/investors/{id}` - Delete investor

### Tasks
- `POST /api/investors/{id}/tasks` - Add task to investor
- `PUT /api/investors/{id}/tasks/{taskId}` - Update task
- `DELETE /api/investors/{id}/tasks/{taskId}` - Delete task

### User Management (Admin Only)
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user
- `GET /api/users/{id}/login-stats` - Get user login statistics

## Database Structure

The application uses SQL Server with Entity Framework Core. Main tables:

- `Users` - User accounts and authentication
- `Investors` - Investor records with contact info and metadata
- `InvestorTasks` - Tasks associated with investors
- `SysLog` - System logging (login events, pageviews, errors)

## Security Features

- Magic link email authentication (no passwords stored)
- HTTP-only, Secure session cookies
- Rate limiting on login attempts (5 attempts per 15 minutes per IP)
- RowVersion-based concurrency control for data integrity
- HTTPS enforcement (configure in Azure App Service)
- Admin-only user management endpoints

## Development Notes

- Session tokens use HMAC-SHA256 signing (simple/lean approach)
- Sessions stored in-memory with sliding 7-day expiry
- Rate limiting uses in-memory dictionary (consider Redis for scale)
- CORS is permissive in development, restricted in production
- Login and pageview events are logged to SysLog table
- Admin users can view login statistics for all users

## Deployment

See [Docs/10-deployment.md](Docs/10-deployment.md) for detailed instructions on deploying to Azure App Service using GitHub Actions.

Quick steps:
1. Create Azure App Service and required resources
2. Configure GitHub secrets with Azure publish profile
3. Update workflow file with your app name
4. Push to main branch to trigger automatic deployment

## License

Internal use only.
