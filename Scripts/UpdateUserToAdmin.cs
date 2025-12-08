using System;
using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Scripts;

class UpdateUserToAdmin
{
    static void Main(string[] args)
    {
        var email = args.Length > 0 ? args[0] : "phil@lamberts.com";

        Console.WriteLine($"Updating user {email} to admin...");

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

        try
        {
            using var connection = new SqlConnection(dbConnectionString);
            connection.Open();

            // Normalize email to lowercase
            var normalizedEmail = email.ToLowerInvariant();

            // Check if user exists
            var checkCmd = new SqlCommand(@"
                SELECT Id, Username, DisplayName, IsAdmin
                FROM Users
                WHERE Username = @email OR Id = @email", connection);
            checkCmd.Parameters.AddWithValue("@email", normalizedEmail);

            using var reader = checkCmd.ExecuteReader();
            if (!reader.HasRows)
            {
                Console.Error.WriteLine($"User with email {email} not found in database.");
                Environment.Exit(1);
            }

            reader.Read();
            var userId = reader.GetString(0);
            var username = reader.GetString(1);
            var displayName = reader.GetString(2);
            var isAdmin = reader.GetBoolean(3);
            reader.Close();

            if (isAdmin)
            {
                Console.WriteLine($"User {email} is already an admin.");
                return;
            }

            // Update user to admin
            var updateCmd = new SqlCommand(@"
                UPDATE Users
                SET IsAdmin = 1
                WHERE Id = @userId", connection);
            updateCmd.Parameters.AddWithValue("@userId", userId);
            updateCmd.ExecuteNonQuery();

            Console.WriteLine($"✓ User {email} ({displayName}) has been updated to admin successfully!");
            Console.WriteLine($"  User ID: {userId}");
            Console.WriteLine($"  Username: {username}");
            Console.WriteLine($"  Display Name: {displayName}");
            Console.WriteLine($"  Is Admin: True");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
