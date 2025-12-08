# Test Email Configuration Script
# Tests the /api/test-email endpoint to verify email sending capability

param(
    [Parameter(Mandatory=$false)]
    [string]$Email = "test@example.com",
    
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "https://iris.intralogichealth.com"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Email Configuration Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$testUrl = "$BaseUrl/api/test-email?email=$Email"

Write-Host "Testing endpoint: $testUrl" -ForegroundColor Yellow
Write-Host ""

try {
    Write-Host "Sending request..." -ForegroundColor Gray
    
    # Add headers to handle potential authentication issues
    $headers = @{
        "Accept" = "application/json"
    }
    
    $response = Invoke-RestMethod -Uri $testUrl -Method Get -Headers $headers -ErrorAction Stop
    
    Write-Host "Response received!" -ForegroundColor Green
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Results:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Display message
    if ($response.emailSent) {
        Write-Host "Status: " -NoNewline
        Write-Host "SUCCESS" -ForegroundColor Green
        Write-Host "Message: $($response.message)" -ForegroundColor Green
    } else {
        Write-Host "Status: " -NoNewline
        Write-Host "FAILED" -ForegroundColor Red
        Write-Host "Message: $($response.message)" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Configuration Status:" -ForegroundColor Yellow
    Write-Host "  Connection String: " -NoNewline
    if ($response.configuration.HasConnectionString) {
        Write-Host "Configured" -ForegroundColor Green
    } else {
        Write-Host "NOT Configured" -ForegroundColor Red
    }
    
    Write-Host "  From Email: " -NoNewline
    if ($response.configuration.HasFromEmail) {
        Write-Host "Configured ($($response.configuration.FromEmail))" -ForegroundColor Green
    } else {
        Write-Host "NOT Configured" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Test Details:" -ForegroundColor Yellow
    Write-Host "  Test Email: $($response.testEmail)"
    Write-Host "  Test Link: $($response.testLink)"
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    
    if ($response.emailSent) {
        Write-Host "✓ Email test PASSED - Check your inbox!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "✗ Email test FAILED - Check configuration" -ForegroundColor Red
        Write-Host ""
        Write-Host "To fix:" -ForegroundColor Yellow
        Write-Host "1. Go to Azure Portal → iris-app-corp → Configuration" -ForegroundColor Gray
        Write-Host "2. Add Application Settings:" -ForegroundColor Gray
        Write-Host "   - Email__ConnectionString = your Azure Communication Services connection string" -ForegroundColor Gray
        Write-Host "   - Email__FromEmail = your verified sender email" -ForegroundColor Gray
        Write-Host "3. Click Save to restart the app" -ForegroundColor Gray
        exit 1
    }
    
} catch {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "ERROR: Failed to test email endpoint" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error Details:" -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "HTTP Status: $statusCode" -ForegroundColor Red
        
        if ($statusCode -eq 401) {
            Write-Host ""
            Write-Host "401 Unauthorized - This usually means:" -ForegroundColor Yellow
            Write-Host "- The latest code with test-email endpoint may not be deployed yet" -ForegroundColor Gray
            Write-Host "- Wait a few minutes for the GitHub Actions deployment to complete" -ForegroundColor Gray
            Write-Host "- Check: https://github.com/iphilbo/Iris/actions" -ForegroundColor Cyan
        }
    }
    
    Write-Host ""
    Write-Host "Possible issues:" -ForegroundColor Yellow
    Write-Host "- The app may not be deployed yet (check GitHub Actions)" -ForegroundColor Gray
    Write-Host "- The URL may be incorrect" -ForegroundColor Gray
    Write-Host "- Network connectivity issues" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To test locally, run:" -ForegroundColor Yellow
    Write-Host "  cd RaiseTracker.Api" -ForegroundColor Cyan
    Write-Host "  dotnet run" -ForegroundColor Cyan
    Write-Host "  Then test: http://localhost:5000/api/test-email?email=your@email.com" -ForegroundColor Cyan
    
    exit 1
}

