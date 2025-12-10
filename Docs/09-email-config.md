# Email Configuration

## Sender Email Address

**IMPORTANT: Do not lose this email address!**

```
DoNotReply@7fc0cf7a-6885-48e7-806d-0c3d0d18bd4c.azurecomm.net
```

This is the verified sender address for Azure Communication Services Email.

## Configuration

### Local Development
- File: `Iris.Api/appsettings.Development.json` (gitignored)
- Setting: `Email:FromEmail`

### Production (Azure App Service)
- Resource: `iris-app-corp`
- Resource Group: `corp-prod-rg`
- Setting: `Email__FromEmail`

### Connection String
- Azure Communication Services: `corp-comm-services`
- Endpoint: `https://corp-comm-services.unitedstates.communication.azure.com/`

## Verification

To verify the email configuration is correct:

```powershell
# Check Azure App Service settings
az webapp config appsettings list --name iris-app-corp --resource-group corp-prod-rg --query "[?contains(name, 'Email')]" -o table

# Test email sending
.\test-email.ps1 -Email "your@email.com" -BaseUrl "http://localhost:5253"
```

## Notes

- The domain `7fc0c77a-6885-4e87-806d-0c3d0d18bd4c.azurecomm.net` is the Azure Managed Domain linked to the Communication Service
- This address must be used exactly as shown - it's tied to the Azure Communication Services resource
- If you need to change it, you'll need to link a new domain in Azure Communication Services
