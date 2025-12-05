using System;
using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Scripts;

class ListUsers
{
    static void Main(string[] args)
    {
        Console.WriteLine("Listing all users in database...\n");

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

            var cmd = new SqlCommand(@"
                SELECT Id, Username, DisplayName, IsAdmin
                FROM Users
                ORDER BY Username", connection);

            using var reader = cmd.ExecuteReader();

            if (!reader.HasRows)
            {
                Console.WriteLine("No users found in database.");
                return;
            }

            Console.WriteLine("Users in database:");
            Console.WriteLine("─────────────────────────────────────────────────────────────");
            Console.WriteLine($"{"ID",-40} {"Username",-30} {"Display Name",-20} {"Admin"}");
            Console.WriteLine("─────────────────────────────────────────────────────────────");

            while (reader.Read())
            {
                var id = reader.GetString(0);
                var username = reader.GetString(1);
                var displayName = reader.GetString(2);
                var isAdmin = reader.GetBoolean(3);

                Console.WriteLine($"{id,-40} {username,-30} {displayName,-20} {isAdmin}");
            }

            Console.WriteLine("─────────────────────────────────────────────────────────────");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
