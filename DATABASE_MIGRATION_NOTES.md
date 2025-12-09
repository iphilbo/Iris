# Database Migration Notes

## Connection String Configuration

The SQL Server connection string has been added to the configuration files:

- **Connection String Name**: `DefaultConnection`
- **Database**: Azure SQL Database (`corp-db`)
- **Server**: `sqlserver-corp.database.windows.net`

### Current Configuration

- ✅ Added to `appsettings.json` (with credentials)
- ✅ Added to `appsettings.Development.json` (for local development)

### ⚠️ Security Warning

**IMPORTANT**: The connection string contains production database credentials.

**For Production Deployment:**
- ❌ **DO NOT** commit connection strings with credentials to source control
- ✅ Use **Azure Key Vault** or **Environment Variables** for production
- ✅ Use **Azure App Service Configuration** to set connection strings securely
- ✅ Consider using **Managed Identity** for Azure SQL authentication instead of username/password

### Recommended Production Setup

1. **Azure App Service Configuration**:
   ```
   ConnectionStrings__DefaultConnection = [connection string]
   ```

2. **Or Environment Variable**:
   ```
   ConnectionStrings:DefaultConnection = [connection string]
   ```

3. **Or Azure Key Vault** (most secure):
   - Store connection string in Azure Key Vault
   - Reference from App Service using Key Vault references

## Migration Steps

### 1. Database Schema Creation
- Create tables for:
  - Users
  - Investors
  - Tasks
  - Index/Summary data

### 2. Entity Framework Setup (Recommended)
- Add Entity Framework Core packages:
  ```bash
  dotnet add package Microsoft.EntityFrameworkCore.SqlServer
  dotnet add package Microsoft.EntityFrameworkCore.Design
  ```
- Create DbContext
- Create Entity models
- Generate migrations

### 3. Update Services
- Replace `BlobStorageService` with database-backed service
- Update `IAuthService` to use database
- Migrate data from Azure Blob Storage to SQL Database

### 4. Configuration Updates
- Update `ConfigureRaiseTracker` to register DbContext
- Update connection string references from `AzureStorage` to `DefaultConnection`

## Current State

- ✅ Connection string stored in configuration
- ⚠️ Services still using Azure Blob Storage
- ⚠️ Need to implement database layer
- ⚠️ Need to migrate existing data

---

**Connection String Location**: `appsettings.json` and `appsettings.Development.json`
**Security Status**: ⚠️ Contains credentials - secure before production deployment





