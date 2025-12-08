using System;
using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Scripts;

class DeleteUser
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: DeleteUser <userId or email>");
            Environment.Exit(1);
        }

        var identifier = args[0];
        Console.WriteLine($"Deleting user: {identifier}...");

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

            // Normalize identifier
            var normalizedIdentifier = identifier.ToLowerInvariant();

            // Check if user exists
            var checkCmd = new SqlCommand(@"
                SELECT Id, Username, DisplayName, IsAdmin
                FROM Users
                WHERE Username = @identifier OR Id = @identifier", connection);
            checkCmd.Parameters.AddWithValue("@identifier", normalizedIdentifier);

            using var reader = checkCmd.ExecuteReader();
            if (!reader.HasRows)
            {
                Console.Error.WriteLine($"User '{identifier}' not found in database.");
                Environment.Exit(1);
            }

            reader.Read();
            var userId = reader.GetString(0);
            var username = reader.GetString(1);
            var displayName = reader.GetString(2);
            var isAdmin = reader.GetBoolean(3);
            reader.Close();

            Console.WriteLine($"Found user:");
            Console.WriteLine($"  ID: {userId}");
            Console.WriteLine($"  Username: {username}");
            Console.WriteLine($"  Display Name: {displayName}");
            Console.WriteLine($"  Is Admin: {isAdmin}");
            Console.WriteLine();
            Console.Write("Are you sure you want to delete this user? (yes/no): ");
            var confirmation = Console.ReadLine();

            if (confirmation?.ToLower() != "yes")
            {
                Console.WriteLine("Deletion cancelled.");
                return;
            }

            // Delete user
            var deleteCmd = new SqlCommand(@"
                DELETE FROM Users
                WHERE Id = @userId", connection);
            deleteCmd.Parameters.AddWithValue("@userId", userId);
            var rowsAffected = deleteCmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                Console.WriteLine($"✓ User {username} ({displayName}) has been deleted successfully!");
            }
            else
            {
                Console.WriteLine($"Failed to delete user. No rows affected.");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
