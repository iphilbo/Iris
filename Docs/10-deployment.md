# Azure Deployment Guide

This guide explains how to deploy the Iris application to Azure App Service using GitHub Actions.

## Prerequisites

1. **Azure Account** with an active subscription
2. **Azure App Service** created (Linux or Windows)
3. **Azure SQL Database** (if using database storage)
4. **Azure Storage Account** (if using blob storage)
5. **GitHub Repository** with Actions enabled

## Step 1: Create Azure Resources

### 1.1 Create App Service

```bash
# Using Azure CLI
az webapp create \
  --resource-group <your-resource-group> \
  --plan <your-app-service-plan> \
  --name <your-app-name> \
  --runtime "DOTNET|8.0"
```

Or create via Azure Portal:
- Go to Azure Portal → Create a resource → Web App
- Choose .NET 8.0 runtime stack
- Select your resource group and app service plan

### 1.2 Create SQL Database (if needed)

```bash
az sql server create \
  --name <your-server-name> \
  --resource-group <your-resource-group> \
  --location <your-location> \
  --admin-user <admin-username> \
  --admin-password <admin-password>

az sql db create \
  --resource-group <your-resource-group> \
  --server <your-server-name> \
  --name <your-database-name> \
  --service-objective S0
```

### 1.3 Create Storage Account (if using blob storage)

```bash
az storage account create \
  --name <your-storage-account> \
  --resource-group <your-resource-group> \
  --location <your-location> \
  --sku Standard_LRS
```

## Step 2: Configure Azure App Service

### 2.1 Get Publish Profile

1. Go to Azure Portal → Your App Service → Overview
2. Click "Get publish profile"
3. Download the `.PublishSettings` file
4. Open the file and copy the `publishProfile` XML content

### 2.2 Configure Connection Strings

In Azure Portal → Your App Service → Configuration → Connection strings:

Add the following connection strings:
- **Name**: `DefaultConnection`
  - **Value**: `Server=tcp:<your-server>.database.windows.net,1433;Initial Catalog=<your-database>;Persist Security Info=False;User ID=<your-user>;Password=<your-password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
  - **Type**: SQLAzure

- **Name**: `AzureStorage` (if using blob storage)
  - **Value**: `DefaultEndpointsProtocol=https;AccountName=<your-account>;AccountKey=<your-key>;EndpointSuffix=core.windows.net`
  - **Type**: Custom

### 2.3 Configure Application Settings

In Azure Portal → Your App Service → Configuration → Application settings:

Add:
- **Name**: `SessionSigningKey`
  - **Value**: Generate a secure random key (e.g., use `openssl rand -base64 32`)

- **Name**: `ASPNETCORE_ENVIRONMENT`
  - **Value**: `Production`

## Step 3: Configure GitHub Secrets

1. Go to your GitHub repository
2. Navigate to Settings → Secrets and variables → Actions
3. Add the following secret:

   - **Name**: `AZURE_WEBAPP_PUBLISH_PROFILE`
   - **Value**: Paste the entire content of the `.PublishSettings` file (the `publishProfile` XML)

## Step 4: Update Workflow File

Edit `.github/workflows/azure-deploy.yml` and update:

```yaml
env:
  AZURE_WEBAPP_NAME: your-actual-app-name    # Replace with your Azure App Service name
```

## Step 5: Push to Main Branch

Once you push to the `main` or `master` branch, the workflow will automatically:

1. Build the .NET 8.0 application
2. Publish the application
3. Deploy to Azure App Service

## Manual Deployment

You can also trigger the workflow manually:

1. Go to GitHub → Actions tab
2. Select "Build and Deploy to Azure"
3. Click "Run workflow"

## Troubleshooting

### Build Fails

- Check that the project path in the workflow matches your project structure
- Verify .NET 8.0 SDK is available in the workflow

### Deployment Fails

- Verify the `AZURE_WEBAPP_PUBLISH_PROFILE` secret is correctly set
- Check that the app name in the workflow matches your Azure App Service name
- Ensure the publish profile hasn't expired (regenerate if needed)

### Application Errors After Deployment

- Check Application Insights or logs in Azure Portal
- Verify connection strings are correctly configured
- Ensure all required environment variables are set
- Check that the database schema is up to date

## Database Migrations

If you're using Entity Framework migrations, you may need to run them manually or add a migration step to the workflow:

```yaml
- name: Run database migrations
  run: dotnet ef database update --project ${{ env.PROJECT_PATH }}
  env:
    ConnectionStrings__DefaultConnection: ${{ secrets.AZURE_SQL_CONNECTION_STRING }}
```

## Security Best Practices

1. **Never commit secrets** to the repository
2. **Use Azure Key Vault** for sensitive configuration in production
3. **Enable HTTPS only** in Azure App Service settings
4. **Use managed identities** instead of connection strings when possible
5. **Rotate secrets regularly**

## Monitoring

After deployment, monitor your application:

1. **Application Insights**: Enable in Azure Portal
2. **Log Stream**: View real-time logs in Azure Portal
3. **Metrics**: Monitor CPU, memory, and request metrics

## Rollback

If you need to rollback:

1. Go to Azure Portal → Your App Service → Deployment Center
2. Select the previous deployment
3. Click "Redeploy"

Or use the Azure CLI:

```bash
az webapp deployment source sync \
  --name <your-app-name> \
  --resource-group <your-resource-group>
```
