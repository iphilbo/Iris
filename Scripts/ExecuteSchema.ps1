# PowerShell script to execute CreateDatabaseSchema.sql on Azure SQL Database
# Usage: .\ExecuteSchema.ps1 -ConnectionString "Server=...;Database=...;User Id=...;Password=..."

param(
    [Parameter(Mandatory=$false)]
    [string]$ConnectionString = ""
)

# If connection string not provided, try to read from appsettings.json
if ([string]::IsNullOrEmpty($ConnectionString)) {
    $appsettingsPath = Join-Path $PSScriptRoot "..\RaiseTracker.Api\appsettings.json"
    if (Test-Path $appsettingsPath) {
        $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
        $ConnectionString = $appsettings.ConnectionStrings.DefaultConnection
    }

    if ([string]::IsNullOrEmpty($ConnectionString)) {
        Write-Error "Connection string not found. Please provide -ConnectionString parameter or ensure appsettings.json contains DefaultConnection."
        exit 1
    }
}

# Read the SQL script
$sqlScriptPath = Join-Path $PSScriptRoot "CreateDatabaseSchema.sql"
if (-not (Test-Path $sqlScriptPath)) {
    Write-Error "SQL script not found at: $sqlScriptPath"
    exit 1
}

$sqlScript = Get-Content $sqlScriptPath -Raw

Write-Host "Executing database schema creation..."
Write-Host "Connection: $($ConnectionString -replace 'Password=[^;]+', 'Password=***')"

try {
    # Parse connection string to extract server and database
    $server = ""
    $database = ""
    $userId = ""
    $password = ""

    $ConnectionString -split ';' | ForEach-Object {
        $pair = $_ -split '=', 2
        if ($pair.Length -eq 2) {
            $key = $pair[0].Trim()
            $value = $pair[1].Trim()

            switch ($key) {
                "Server" { $server = $value -replace '^tcp:', '' }
                "Initial Catalog" { $database = $value }
                "User ID" { $userId = $value }
                "Password" { $password = $value }
            }
        }
    }

    if ([string]::IsNullOrEmpty($server) -or [string]::IsNullOrEmpty($database)) {
        Write-Error "Could not parse connection string. Please ensure it contains Server and Initial Catalog."
        exit 1
    }

    # Use sqlcmd if available
    $sqlcmdPath = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if ($sqlcmdPath) {
        Write-Host "Using sqlcmd to execute script..."
        $tempFile = [System.IO.Path]::GetTempFileName()
        $sqlScript | Out-File -FilePath $tempFile -Encoding UTF8

        # Build sqlcmd command
        $sqlcmdArgs = @(
            "-S", $server,
            "-d", $database,
            "-U", $userId,
            "-P", $password,
            "-i", $tempFile,
            "-b" # Stop on error
        )

        & sqlcmd @sqlcmdArgs
        $exitCode = $LASTEXITCODE

        Remove-Item $tempFile -ErrorAction SilentlyContinue

        if ($exitCode -eq 0) {
            Write-Host "Schema created successfully!" -ForegroundColor Green
        } else {
            Write-Error "Schema creation failed with exit code $exitCode"
            exit $exitCode
        }
    } else {
        # Fallback: Use .NET SqlClient
        Write-Host "sqlcmd not found. Using .NET SqlClient..."

        Add-Type -Path "System.Data.dll"

        $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
        $connection.Open()

        try {
            $command = $connection.CreateCommand()
            $command.CommandText = $sqlScript
            $command.CommandTimeout = 300 # 5 minutes

            $command.ExecuteNonQuery()
            Write-Host "Schema created successfully!" -ForegroundColor Green
        } finally {
            $connection.Close()
        }
    }
} catch {
    Write-Error "Error executing schema: $_"
    exit 1
}
