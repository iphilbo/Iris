# Iris - RaiseTracker Feature

A simple web application for tracking Series A investors and related tasks. Built with .NET 8 minimal API backend and vanilla HTML/JS/CSS frontend.

**Iris** is the project name. **RaiseTracker** is a feature within Iris for tracking investors and related tasks.

## Features

- **Authentication**: Simple password-based auth with 7-day sliding session cookies
- **Investor Management**: Track investors with contact info, stage, category, and commit amounts
- **Task Management**: Add and manage tasks per investor
- **Dark/Light Mode**: Toggle between themes with persistent preference
- **Mobile Responsive**: Works on desktop, tablet, and mobile devices
- **Azure Blob Storage**: File-based storage using JSON files in Azure Blob Storage

## Prerequisites

- .NET 8 SDK
- Azure Storage Account with a blob container named `seriesa-data`

## Setup

1. **Configure Azure Storage Connection String**

   Edit `RaiseTracker.Api/appsettings.json` and update the connection string:
   ```json
   {
     "ConnectionStrings": {
       "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net"
     }
   }
   ```

2. **Configure Session Signing Key**

   Update the session signing key in `appsettings.json`:
   ```json
   {
     "SessionSigningKey": "CHANGE-THIS-TO-A-RANDOM-SECRET-KEY-IN-PRODUCTION"
   }
   ```

3. **Run the Application**

   ```bash
   cd RaiseTracker.Api
   dotnet run
   ```

   The application will:
   - Create the `seriesa-data` container if it doesn't exist
   - Initialize `users.json` with the default user (Phil / General123)
   - Initialize `index.json` as an empty array
   - Start the web server (typically at http://localhost:5253)

4. **Access the Application**

   Open your browser to `http://localhost:5253`

## Default User

- **Username**: phil
- **Password**: General123

## Project Structure

```
Iris/
├── Docs/                    # Documentation
├── RaiseTracker.Api/        # Backend API (RaiseTraker feature)
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
- `GET /api/users` - Get list of users for login
- `POST /api/login` - Login with userId and password
- `GET /api/session` - Check current session
- `POST /api/logout` - Logout

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

## Storage Structure

The application uses Azure Blob Storage with the following structure:

- `index.json` - Lightweight list of investors for quick loading
- `investors/{id}.json` - Full investor records with tasks
- `users.json` - User accounts and password hashes (server-only)

## Security Features

- Password hashing using BCrypt
- HTTP-only, Secure session cookies
- Rate limiting on login attempts (5 attempts per 15 minutes per IP)
- ETag-based concurrency control for data integrity
- HTTPS enforcement (configure in Azure App Service)

## Development Notes

- Session tokens use HMAC-SHA256 signing (simple/lean approach)
- Sessions stored in-memory with sliding 7-day expiry
- Rate limiting uses in-memory dictionary (consider Redis for scale)
- CORS is permissive in development, restricted in production

## Deployment

1. Update `appsettings.json` with production values
2. Configure Azure App Service with:
   - Connection string for Azure Storage
   - Session signing key as environment variable
   - HTTPS enforcement
3. Deploy the application to Azure App Service

## License

Internal use only.
