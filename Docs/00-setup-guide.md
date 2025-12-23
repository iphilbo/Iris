# Setup Guide - New Machine Installation

This guide will help you set up the Iris application on a new development machine.

## Prerequisites

### Required Software

1. **.NET 8 SDK**
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify installation:
     ```bash
     dotnet --version
     ```
     Should show version 8.x.x

2. **Git**
   - Download from: https://git-scm.com/downloads
   - Verify installation:
     ```bash
     git --version
     ```

3. **Code Editor (Optional but Recommended)**
   - Visual Studio 2022 (Community edition is free)
   - OR Visual Studio Code with C# extension
   - OR JetBrains Rider

### Required Azure Resources

You'll need access to:

1. **Azure SQL Database**
   - Server: `sqlserver-corp.database.windows.net`
   - Database: `corp-db`
   - Connection credentials (obtain from team lead or Azure portal)

2. **Azure Communication Services**
   - Resource: `corp-comm-services`
   - Connection string and FromEmail address (see `Docs/09-email-config.md`)

## Step 1: Clone the Repository

```bash
# Navigate to your development directory
cd C:\Users\YourUsername\source\repos

# Clone the repository
git clone https://github.com/iphilbo/Iris.git

# Navigate into the project
cd Iris
```

## Step 2: Restore Dependencies

```bash
# Restore NuGet packages
dotnet restore

# Or restore for the specific project
cd Iris.Api
dotnet restore
```

## Step 3: Configure Application Settings

### 3.1 Database Connection String

Edit `Iris.Api/appsettings.json` and update the connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:sqlserver-corp.database.windows.net,1433;Initial Catalog=corp-db;Persist Security Info=False;User ID=YOUR_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

**⚠️ Security Note:** For production, use Azure Key Vault or App Service Configuration. Never commit credentials to source control.

### 3.2 Email Configuration (Development)

Edit `Iris.Api/appsettings.Development.json`:

```json
{
  "Email": {
    "ConnectionString": "endpoint=https://corp-comm-services.unitedstates.communication.azure.com/;accesskey=YOUR_ACCESS_KEY",
    "FromEmail": "DoNotReply@7fc0cf7a-6885-48e7-806d-0c3d0d18bd4c.azurecomm.net"
  }
}
```

**Note:** The `appsettings.Development.json` file should be gitignored and contains sensitive credentials. Obtain these values from:
- Team lead
- Azure Portal (Communication Services resource)
- See `Docs/09-email-config.md` for details

### 3.3 Session Signing Key

The session signing key in `appsettings.json` should already be set. If you need to generate a new one:

```bash
# Generate a random key (PowerShell)
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

Or use an online tool to generate a secure random string.

## Step 4: Database Setup

### Option A: Database Already Exists (Recommended)

If the database is already set up in Azure, you can skip this step. The application will connect automatically.

### Option B: Create Database Schema Manually

If you need to create the database schema manually:

1. Connect to Azure SQL Database using SQL Server Management Studio (SSMS) or Azure Data Studio
2. Run the schema script: `Scripts/CreateDatabaseSchema.sql`
3. Verify tables were created:
   - Users
   - Investors
   - InvestorTasks
   - SysLog

## Step 5: Build the Application

```bash
# From the project root
cd Iris.Api

# Build the project
dotnet build

# Verify no errors
```

## Step 6: Run the Application

```bash
# From Iris.Api directory
dotnet run
```

The application will:
- Start the web server (typically at `http://localhost:5253`)
- Connect to the SQL Server database
- Initialize services

### Expected Output

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5253
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

## Step 7: Access the Application

1. Open your browser to: `http://localhost:5253`
2. You should see the login screen
3. Enter your email address (must be a user in the database)
4. Check your email for the magic link
5. Click the magic link to log in

## Step 8: Verify Setup

### Test Database Connection

The application should connect to the database automatically. If you see connection errors:

1. Verify the connection string is correct
2. Check that your IP is allowed in Azure SQL firewall rules
3. Verify credentials are correct

### Test Email Functionality

1. Request a magic link with your email
2. Check your inbox for the email
3. If emails aren't arriving:
   - Verify Azure Communication Services connection string
   - Check spam folder
   - Verify FromEmail address is correct

### Test Application Features

1. **Login**: Request and use magic link
2. **Investors**: Create, edit, delete an investor
3. **Tasks**: Add tasks to investors
4. **User Management** (if admin): Access user management screen

## Troubleshooting

### Issue: "Unable to connect to database"

**Solutions:**
- Verify connection string in `appsettings.json`
- Check Azure SQL firewall allows your IP address
- Verify database credentials
- Test connection using SSMS or Azure Data Studio

### Issue: "Email not sending"

**Solutions:**
- Verify `appsettings.Development.json` has correct email configuration
- Check Azure Communication Services connection string
- Verify FromEmail address matches the verified domain
- Check application logs for email errors

### Issue: "Port already in use"

**Solutions:**
- Change the port in `Properties/launchSettings.json`
- Or stop the process using port 5253:
  ```bash
  # Windows PowerShell
  netstat -ano | findstr :5253
  taskkill /PID <PID> /F
  ```

### Issue: "Package restore failed"

**Solutions:**
- Clear NuGet cache: `dotnet nuget locals all --clear`
- Restore again: `dotnet restore`
- Check internet connection
- Verify NuGet feed access

### Issue: "Build errors"

**Solutions:**
- Clean solution: `dotnet clean`
- Restore packages: `dotnet restore`
- Rebuild: `dotnet build`
- Check that .NET 8 SDK is installed correctly

## Development Tips

### Running in Development Mode

The application includes a dev auto-login feature for localhost:

- Visit `http://localhost:5253/api/dev-auto-login`
- Automatically logs in as the first admin user
- Only works on localhost/127.0.0.1

### Hot Reload

Enable hot reload for faster development:

```bash
dotnet watch run
```

Changes to code will automatically restart the application.

### Debugging

1. Open the solution in Visual Studio
2. Set breakpoints in your code
3. Press F5 to start debugging
4. The debugger will attach automatically

## Next Steps

After setup is complete:

1. Review the documentation in the `Docs/` folder
2. Familiarize yourself with the API endpoints (see `Docs/05-api-specification.md`)
3. Review the data model (see `Docs/02-data-model.md`)
4. Check deployment guide if deploying (see `Docs/10-deployment.md`)

## Getting Help

- Review documentation in `Docs/` folder
- Check application logs for error messages
- Contact team lead for Azure resource access
- See `README.md` for quick reference

---

**Last Updated:** 2024
**Maintained By:** Development Team

