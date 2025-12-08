using System;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Scripts;

class CheckDatabaseData
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Checking database for existing data...");

        // Read configuration
        var appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Iris.Api", "appsettings.json");
        if (!File.Exists(appsettingsPath))
        {
            Console.Error.WriteLine($"appsettings.json not found at: {appsettingsPath}");
            Environment.Exit(1);
        }

        var appsettingsJson = File.ReadAllText(appsettingsPath);
        var appsettings = JsonSerializer.Deserialize<JsonElement>(appsettingsJson);
        var dbConnectionString = appsettings.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();

        if (string.IsNullOrEmpty(dbConnectionString))
        {
            Console.Error.WriteLine("DefaultConnection not found in appsettings.json");
            Environment.Exit(1);
        }

        // Check if data exists in database
        using var dbConnection = new SqlConnection(dbConnectionString);
        await dbConnection.OpenAsync();

        var checkUsersCmd = new SqlCommand("SELECT COUNT(*) FROM Users", dbConnection);
        var userCount = (int)await checkUsersCmd.ExecuteScalarAsync();

        var checkInvestorsCmd = new SqlCommand("SELECT COUNT(*) FROM Investors", dbConnection);
        var investorCount = (int)await checkInvestorsCmd.ExecuteScalarAsync();

        var checkTasksCmd = new SqlCommand("SELECT COUNT(*) FROM InvestorTasks", dbConnection);
        var taskCount = (int)await checkTasksCmd.ExecuteScalarAsync();

        Console.WriteLine($"Current database state:");
        Console.WriteLine($"  Users: {userCount}");
        Console.WriteLine($"  Investors: {investorCount}");
        Console.WriteLine($"  Tasks: {taskCount}");
        Console.WriteLine();

        if (userCount > 0 || investorCount > 0)
        {
            Console.WriteLine("✓ Data already exists in database!");
            Console.WriteLine("Migration appears to have been completed (or data was added manually).");
        }
        else
        {
            Console.WriteLine("⚠ No data found in database.");
            Console.WriteLine("To migrate data from blob storage, you need:");
            Console.WriteLine("  1. A valid Azure Storage connection string in appsettings.json");
            Console.WriteLine("  2. Run the MigrateBlobToDatabase tool");
        }
    }
}
