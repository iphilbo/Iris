using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;

var baseDir = AppContext.BaseDirectory;
var appsettingsPath = Path.Combine(baseDir, "..", "..", "..", "..", "Iris.Api", "appsettings.json");
if (!File.Exists(appsettingsPath))
{
    // Try alternative path
    appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Iris.Api", "appsettings.json");
}

if (!File.Exists(appsettingsPath))
{
    Console.Error.WriteLine($"appsettings.json not found. Tried: {appsettingsPath}");
    Environment.Exit(1);
}

var appsettingsJson = File.ReadAllText(appsettingsPath);
var appsettings = JsonSerializer.Deserialize<JsonElement>(appsettingsJson);
var connectionString = appsettings.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();

if (string.IsNullOrEmpty(connectionString))
{
    Console.Error.WriteLine("DefaultConnection not found in appsettings.json");
    Environment.Exit(1);
}

var sqlScriptPath = Path.Combine(AppContext.BaseDirectory, "CreateDatabaseSchema.sql");
if (!File.Exists(sqlScriptPath))
{
    // Try alternative path
    sqlScriptPath = Path.Combine(Directory.GetCurrentDirectory(), "CreateDatabaseSchema.sql");
}

if (!File.Exists(sqlScriptPath))
{
    Console.Error.WriteLine($"SQL script not found. Tried: {sqlScriptPath}");
    Environment.Exit(1);
}

var sqlScript = File.ReadAllText(sqlScriptPath);

Console.WriteLine("Executing database schema creation...");
var maskedConnection = connectionString.Replace("Password=", "Password=***").Replace(";Password=", ";Password=***");
Console.WriteLine($"Connection: {maskedConnection}");
Console.WriteLine();

try
{
    using var connection = new SqlConnection(connectionString);
    connection.Open();
    Console.WriteLine("Connected to database successfully.");

    // Split script by GO statements (SQL Server batch separator)
    var batches = System.Text.RegularExpressions.Regex.Split(
        sqlScript,
        @"^\s*GO\s*$",
        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
    );

    foreach (var batch in batches)
    {
        var trimmedBatch = batch.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBatch) || trimmedBatch.StartsWith("--"))
            continue;

        try
        {
            using var command = new SqlCommand(trimmedBatch, connection);
            command.CommandTimeout = 300; // 5 minutes
            command.ExecuteNonQuery();
            Console.WriteLine("✓ Executed batch successfully");
        }
        catch (SqlException ex) when (ex.Number == 2714 || ex.Number == 3701)
        {
            // Object already exists or doesn't exist - this is OK for DROP statements
            Console.WriteLine($"⚠ {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Error executing batch: {ex.Message}");
            throw;
        }
    }

    Console.WriteLine();
    Console.WriteLine("✓ Schema created successfully!");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"✗ Error executing schema: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}
