# Iris - RaiseTracker Feature Development Helper Script

# Common development tasks and shortcuts for the Iris project

param(
    [Parameter(Position = 0)]
    [string]$Command,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'

function Show-Help {
    Write-Host "Iris - RaiseTracker Feature Development Helper" -ForegroundColor Green
    Write-Host "===============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: .\dev-helper.ps1 [command]" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Commands:" -ForegroundColor Yellow
    Write-Host "  build     - Build the project (stops running instances first)" -ForegroundColor White
    Write-Host "  run       - Run the application" -ForegroundColor White
    Write-Host "  stop      - Stop the running application" -ForegroundColor White
    Write-Host "  clean     - Clean build artifacts" -ForegroundColor White
    Write-Host "  watch     - Run with file watching" -ForegroundColor White
    Write-Host "  db-check  - Check database connection and data" -ForegroundColor White
    Write-Host "  status    - Show project status" -ForegroundColor White
    Write-Host "  help      - Show this help" -ForegroundColor White
}

function Invoke-Build {
    Write-Host "Building Iris (RaiseTracker feature)..." -ForegroundColor Cyan

    # Stop any running instances to prevent file locking issues
    Write-Host "Checking for running instances..." -ForegroundColor Yellow
    $runningProcesses = @()

    # Check for PID file first
    if (Test-Path ".raisetracker.pid") {
        try {
            $appPid = Get-Content ".raisetracker.pid" -ErrorAction Stop
            if ($appPid) {
                $p = Get-Process -Id ([int]$appPid) -ErrorAction SilentlyContinue
                if ($p) {
                    $runningProcesses += $p
                }
            }
        }
        catch {}
    }

    # Check for dotnet processes running RaiseTracker
    try {
        $dotnetMatches = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -match "RaiseTracker\.Api" }
        if ($dotnetMatches) {
            foreach ($m in $dotnetMatches) {
                $p = Get-Process -Id $m.ProcessId -ErrorAction SilentlyContinue
                if ($p) {
                    $runningProcesses += $p
                }
            }
        }
    }
    catch {}

    # Check for direct RaiseTracker.Api.exe processes
    try {
        $raiseTrackerProcesses = Get-Process -Name "RaiseTracker.Api" -ErrorAction SilentlyContinue
        if ($raiseTrackerProcesses) {
            $runningProcesses += $raiseTrackerProcesses
        }
    }
    catch {}

    if ($runningProcesses.Count -gt 0) {
        Write-Host "Found $($runningProcesses.Count) running Iris process(es), stopping..." -ForegroundColor Yellow
        foreach ($proc in $runningProcesses) {
            try {
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                Write-Host "Stopped process PID $($proc.Id)" -ForegroundColor Gray
            }
            catch {
                Write-Host "Could not stop process PID $($proc.Id)" -ForegroundColor Yellow
            }
        }
        Start-Sleep -Seconds 2  # Give time for cleanup
        Remove-Item ".raisetracker.pid" -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "No running Iris processes found" -ForegroundColor Gray
    }

    # Now build
    dotnet build RaiseTracker.Api --nologo --verbosity minimal
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!" -ForegroundColor Green
    }
    else {
        Write-Host "Build failed!" -ForegroundColor Red
    }
}

function Invoke-Run {
    Write-Host "Starting Iris API (RaiseTracker feature)..." -ForegroundColor Cyan
    Write-Host "   Application will be available at:" -ForegroundColor Gray
    Write-Host "   - http://localhost:5253" -ForegroundColor Gray
    Write-Host ""

    # Stop any existing instances first
    Write-Host "Stopping any existing instances..." -ForegroundColor Yellow
    Invoke-Stop
    Start-Sleep -Seconds 2  # Give time for cleanup

    # Start the application in background and persist PID
    $proc = Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run", "--project", "RaiseTracker.Api" -PassThru
    if ($proc -and $proc.Id) {
        Set-Content -Path ".raisetracker.pid" -Value $proc.Id -Encoding ASCII -NoNewline
        Write-Host "Application started (PID: $($proc.Id))" -ForegroundColor Green
    }

    # Wait a moment for the app to start
    Write-Host "Waiting for application to start..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5

    # Open default browser
    Write-Host "Opening default browser..." -ForegroundColor Green
    Start-Process "http://localhost:5253/raise-tracker.html"

    Write-Host "Application started! Browser should open automatically." -ForegroundColor Green
    Write-Host "Press Ctrl+C in the terminal to stop the application." -ForegroundColor Gray
    Write-Host "Or run: .\dev-helper.ps1 stop" -ForegroundColor Gray
}

function Invoke-Clean {
    Write-Host "Cleaning build artifacts..." -ForegroundColor Cyan
    dotnet clean RaiseTracker.Api --nologo --verbosity minimal
    Write-Host "Clean complete!" -ForegroundColor Green
}

function Invoke-Watch {
    Write-Host "Starting with file watching..." -ForegroundColor Cyan
    dotnet watch --project RaiseTracker.Api
}

function Invoke-DbCheck {
    Write-Host "Checking database connection and data..." -ForegroundColor Cyan

    if (Test-Path "RaiseTracker.Api/appsettings.Development.json") {
        $settings = Get-Content "RaiseTracker.Api/appsettings.Development.json" | ConvertFrom-Json
        if ($settings.ConnectionStrings.DefaultConnection) {
            Write-Host "Database connection string found" -ForegroundColor Green
            $connStr = $settings.ConnectionStrings.DefaultConnection
            $masked = $connStr -replace 'Password=[^;]+', 'Password=***'
            Write-Host "   Connection: $($masked.Substring(0, [Math]::Min(80, $masked.Length)))..." -ForegroundColor Gray

            # Try to check database
            Write-Host "`nChecking database data..." -ForegroundColor Yellow
            Push-Location "Scripts"
            try {
                dotnet run --project CheckDatabaseData.csproj --no-build 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    dotnet run --project CheckDatabaseData.csproj
                }
                else {
                    Write-Host "Could not check database (build required)" -ForegroundColor Yellow
                }
            }
            catch {
                Write-Host "Could not check database: $_" -ForegroundColor Yellow
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-Host "No DefaultConnection found" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "appsettings.Development.json not found" -ForegroundColor Red
    }
}

function Invoke-Stop {
    Write-Host "Stopping Iris application..." -ForegroundColor Cyan

    # Prefer PID file if present
    $stopped = $false
    if (Test-Path ".raisetracker.pid") {
        try {
            $appPid = Get-Content ".raisetracker.pid" -ErrorAction Stop
            if ($appPid) {
                $p = Get-Process -Id ([int]$appPid) -ErrorAction SilentlyContinue
                if ($p) {
                    Write-Host "Stopping process PID $appPid" -ForegroundColor Yellow
                    Stop-Process -Id $appPid -Force -ErrorAction SilentlyContinue
                    $stopped = $true
                }
            }
        }
        catch {}
        Remove-Item ".raisetracker.pid" -ErrorAction SilentlyContinue
    }

    if (-not $stopped) {
        # Fallback: stop dotnet processes matching RaiseTracker.Api
        try {
            $dotnetMatches = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -match "RaiseTracker\.Api" }
        }
        catch { $dotnetMatches = @() }

        if ($dotnetMatches -and $dotnetMatches.Count -gt 0) {
            Write-Host "Found $($dotnetMatches.Count) matching process(es), stopping..." -ForegroundColor Yellow
            foreach ($m in $dotnetMatches) {
                Stop-Process -Id $m.ProcessId -Force -ErrorAction SilentlyContinue
            }
            $stopped = $true
        }
    }

    if ($stopped) {
        Write-Host "Application stopped!" -ForegroundColor Green
    }
    else {
        Write-Host "No running Iris process found." -ForegroundColor Gray
    }
}

function Invoke-Status {
    Write-Host "Iris Project Status" -ForegroundColor Cyan
    Write-Host "===================" -ForegroundColor Cyan

    # Check .NET version
    $dotnetVersion = dotnet --version
    Write-Host ".NET Version: $dotnetVersion" -ForegroundColor White

    # Check if solution builds
    Write-Host "Checking build status..." -ForegroundColor Gray
    dotnet build RaiseTracker.Api --nologo --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Solution builds successfully" -ForegroundColor Green
    }
    else {
        Write-Host "Solution has build errors" -ForegroundColor Red
    }

    # Check for key files and folders
    $keyFiles = @(
        "Iris.sln",
        "RaiseTracker.Api/RaiseTracker.Api.csproj",
        "RaiseTracker.Api/Program.cs",
        "RaiseTracker.Api/appsettings.json",
        "RaiseTracker.Api/appsettings.Development.json"
    )

    Write-Host "`nKey files:" -ForegroundColor Gray
    foreach ($file in $keyFiles) {
        if (Test-Path $file) {
            Write-Host "  OK $file" -ForegroundColor Green
        }
        else {
            Write-Host "  Missing $file" -ForegroundColor Red
        }
    }

    # Check if application is running
    Write-Host "`nRunning processes:" -ForegroundColor Gray
    if (Test-Path ".raisetracker.pid") {
        $pid = Get-Content ".raisetracker.pid" -ErrorAction SilentlyContinue
        $proc = Get-Process -Id ([int]$pid) -ErrorAction SilentlyContinue
        if ($proc) {
            Write-Host "  Running (PID: $pid)" -ForegroundColor Green
        }
        else {
            Write-Host "  Not running (stale PID file)" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "  Not running" -ForegroundColor Gray
    }
}

# Main script logic
switch ($Command.ToLower()) {
    "build" { Invoke-Build }
    "run" { Invoke-Run }
    "stop" { Invoke-Stop }
    "clean" { Invoke-Clean }
    "watch" { Invoke-Watch }
    "db-check" { Invoke-DbCheck }
    "status" { Invoke-Status }
    "help" { Show-Help }
    "" { Show-Help }
    default {
        Write-Host "Unknown command: $Command" -ForegroundColor Red
        Write-Host ""
        Show-Help
    }
}
