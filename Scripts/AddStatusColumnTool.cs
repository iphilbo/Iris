using System;
using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Scripts;

class AddStatusColumnTool
{
    static void Main(string[] args)
    {
        Console.WriteLine("Adding Status column to Investors table...");

        // Read configuration
        var appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "RaiseTracker.Api", "appsettings.json");
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

        try
        {
            using var connection = new SqlConnection(dbConnectionString);
            connection.Open();

            // Check if column exists
            var checkCmd = new SqlCommand(@"
                SELECT COUNT(*)
                FROM sys.columns
                WHERE object_id = OBJECT_ID('Investors') AND name = 'Status'", connection);
            var exists = (int)checkCmd.ExecuteScalar() > 0;

            if (exists)
            {
                Console.WriteLine("Status column already exists.");
            }
            else
            {
                // Add the column
                var addCmd = new SqlCommand(@"
                    ALTER TABLE Investors
                    ADD Status NVARCHAR(50) NOT NULL DEFAULT 'Active'", connection);
                addCmd.ExecuteNonQuery();
                Console.WriteLine("✓ Status column added successfully!");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
