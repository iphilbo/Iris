using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;

namespace Scripts;

class CheckAndMigrateData
{
    static async Task Main(string[] args)
    {
        await RunCheckAndMigrate();
    }

    static async Task RunCheckAndMigrate()
    {
        Console.WriteLine("Checking database for existing data...");

        // Read configuration
        var appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "RaiseTracker.Api", "appsettings.json");
        if (!File.Exists(appsettingsPath))
        {
            Console.Error.WriteLine($"appsettings.json not found at: {appsettingsPath}");
            Environment.Exit(1);
        }

        var appsettingsJson = File.ReadAllText(appsettingsPath);
        var appsettings = JsonSerializer.Deserialize<JsonElement>(appsettingsJson);
        var connectionStrings = appsettings.GetProperty("ConnectionStrings");

        var blobConnectionString = connectionStrings.GetProperty("AzureStorage").GetString();
        var dbConnectionString = connectionStrings.GetProperty("DefaultConnection").GetString();

        if (string.IsNullOrEmpty(blobConnectionString) || string.IsNullOrEmpty(dbConnectionString))
        {
            Console.Error.WriteLine("Connection strings not found in appsettings.json");
            Environment.Exit(1);
        }

        // Check if data exists in database
        using var dbConnection = new SqlConnection(dbConnectionString);
        await dbConnection.OpenAsync();

        var checkUsersCmd = new SqlCommand("SELECT COUNT(*) FROM Users", dbConnection);
        var userCount = (int)await checkUsersCmd.ExecuteScalarAsync();

        var checkInvestorsCmd = new SqlCommand("SELECT COUNT(*) FROM Investors", dbConnection);
        var investorCount = (int)await checkInvestorsCmd.ExecuteScalarAsync();

        Console.WriteLine($"Current database state:");
        Console.WriteLine($"  Users: {userCount}");
        Console.WriteLine($"  Investors: {investorCount}");
        Console.WriteLine();

        if (userCount > 0 || investorCount > 0)
        {
            Console.WriteLine("✓ Data already exists in database. Migration may have already been completed.");
            Console.WriteLine("If you want to re-run migration, please clear the database first.");
            return;
        }

        Console.WriteLine("⚠ No data found in database. Starting migration from blob storage...");
        Console.WriteLine();

        // Run migration
        const string containerName = "seriesa-data";
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var blobServiceClient = new BlobServiceClient(blobConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        if (!await containerClient.ExistsAsync())
        {
            Console.Error.WriteLine($"Container '{containerName}' does not exist in blob storage.");
            Environment.Exit(1);
        }

        Console.WriteLine("✓ Connected to blob storage");

        // Migrate users
        Console.WriteLine("Migrating users...");
        var usersBlob = containerClient.GetBlobClient("users.json");
        if (await usersBlob.ExistsAsync())
        {
            var usersResponse = await usersBlob.DownloadContentAsync();
            var usersJson = usersResponse.Value.Content.ToString();
            var users = JsonSerializer.Deserialize<JsonElement[]>(usersJson, jsonOptions) ?? Array.Empty<JsonElement>();

            foreach (var user in users)
            {
                var insertCmd = new SqlCommand(@"
                    INSERT INTO Users (Id, Username, DisplayName, PasswordHash, IsAdmin)
                    VALUES (@Id, @Username, @DisplayName, @PasswordHash, @IsAdmin)", dbConnection);

                insertCmd.Parameters.AddWithValue("@Id", user.GetProperty("id").GetString());
                insertCmd.Parameters.AddWithValue("@Username", user.GetProperty("username").GetString());
                insertCmd.Parameters.AddWithValue("@DisplayName", user.GetProperty("displayName").GetString());
                insertCmd.Parameters.AddWithValue("@PasswordHash", user.GetProperty("passwordHash").GetString());
                insertCmd.Parameters.AddWithValue("@IsAdmin", user.GetProperty("isAdmin").GetBoolean());

                await insertCmd.ExecuteNonQueryAsync();
                Console.WriteLine($"  + Migrated user: {user.GetProperty("username").GetString()}");
            }

            Console.WriteLine($"✓ Migrated {users.Length} users");
        }

        // Migrate investors and tasks
        Console.WriteLine("Migrating investors...");
        var indexBlob = containerClient.GetBlobClient("index.json");
        var investorIds = new List<string>();

        if (await indexBlob.ExistsAsync())
        {
            var indexResponse = await indexBlob.DownloadContentAsync();
            var indexJson = indexResponse.Value.Content.ToString();
            var index = JsonSerializer.Deserialize<JsonElement[]>(indexJson, jsonOptions) ?? Array.Empty<JsonElement>();
            investorIds = index.Select(i => i.GetProperty("id").GetString()).ToList();
        }
        else
        {
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: "investors/"))
            {
                if (blobItem.Name.EndsWith(".json"))
                {
                    investorIds.Add(Path.GetFileNameWithoutExtension(blobItem.Name));
                }
            }
        }

        Console.WriteLine($"  Found {investorIds.Count} investors to migrate");

        int migrated = 0;
        foreach (var investorId in investorIds)
        {
            try
            {
                var investorBlob = containerClient.GetBlobClient($"investors/{investorId}.json");
                if (!await investorBlob.ExistsAsync()) continue;

                var investorResponse = await investorBlob.DownloadContentAsync();
                var investorJson = investorResponse.Value.Content.ToString();
                var investor = JsonSerializer.Deserialize<JsonElement>(investorJson, jsonOptions);

                // Insert investor
                var investorCmd = new SqlCommand(@"
                    INSERT INTO Investors (Id, Name, MainContact, ContactEmail, ContactPhone, Category, Stage, CommitAmount, Notes, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                    VALUES (@Id, @Name, @MainContact, @ContactEmail, @ContactPhone, @Category, @Stage, @CommitAmount, @Notes, @CreatedBy, @CreatedAt, @UpdatedBy, @UpdatedAt)", dbConnection);

                investorCmd.Parameters.AddWithValue("@Id", investorId);
                investorCmd.Parameters.AddWithValue("@Name", investor.GetProperty("name").GetString());
                investorCmd.Parameters.AddWithValue("@MainContact", (object?)investor.TryGetProperty("mainContact", out var mc) ? mc.GetString() : DBNull.Value);
                investorCmd.Parameters.AddWithValue("@ContactEmail", (object?)investor.TryGetProperty("contactEmail", out var ce) ? ce.GetString() : DBNull.Value);
                investorCmd.Parameters.AddWithValue("@ContactPhone", (object?)investor.TryGetProperty("contactPhone", out var cp) ? cp.GetString() : DBNull.Value);
                investorCmd.Parameters.AddWithValue("@Category", investor.GetProperty("category").GetString());
                investorCmd.Parameters.AddWithValue("@Stage", investor.GetProperty("stage").GetString());
                investorCmd.Parameters.AddWithValue("@CommitAmount", (object?)investor.TryGetProperty("commitAmount", out var ca) && ca.ValueKind != JsonValueKind.Null ? ca.GetDecimal() : DBNull.Value);
                investorCmd.Parameters.AddWithValue("@Notes", (object?)investor.TryGetProperty("notes", out var n) ? n.GetString() : DBNull.Value);
                investorCmd.Parameters.AddWithValue("@CreatedBy", (object?)investor.TryGetProperty("createdBy", out var cb) ? cb.GetString() : DBNull.Value);
                investorCmd.Parameters.AddWithValue("@CreatedAt", (object?)investor.TryGetProperty("createdAt", out var cdt) && cdt.ValueKind != JsonValueKind.Null ? DateTime.Parse(cdt.GetString()) : DBNull.Value);
                investorCmd.Parameters.AddWithValue("@UpdatedBy", (object?)investor.TryGetProperty("updatedBy", out var ub) ? ub.GetString() : DBNull.Value);
                investorCmd.Parameters.AddWithValue("@UpdatedAt", (object?)investor.TryGetProperty("updatedAt", out var udt) && udt.ValueKind != JsonValueKind.Null ? DateTime.Parse(udt.GetString()) : DBNull.Value);

                await investorCmd.ExecuteNonQueryAsync();

                // Insert tasks
                if (investor.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var task in tasks.EnumerateArray())
                    {
                        var taskCmd = new SqlCommand(@"
                            INSERT INTO InvestorTasks (Id, InvestorId, Description, DueDate, Done, CreatedAt, UpdatedAt)
                            VALUES (@Id, @InvestorId, @Description, @DueDate, @Done, @CreatedAt, @UpdatedAt)", dbConnection);

                        taskCmd.Parameters.AddWithValue("@Id", task.GetProperty("id").GetString());
                        taskCmd.Parameters.AddWithValue("@InvestorId", investorId);
                        taskCmd.Parameters.AddWithValue("@Description", task.GetProperty("description").GetString());
                        taskCmd.Parameters.AddWithValue("@DueDate", task.GetProperty("dueDate").GetString());
                        taskCmd.Parameters.AddWithValue("@Done", task.GetProperty("done").GetBoolean());
                        taskCmd.Parameters.AddWithValue("@CreatedAt", (object?)task.TryGetProperty("createdAt", out var tcdt) && tcdt.ValueKind != JsonValueKind.Null ? DateTime.Parse(tcdt.GetString()) : DBNull.Value);
                        taskCmd.Parameters.AddWithValue("@UpdatedAt", (object?)task.TryGetProperty("updatedAt", out var tudt) && tudt.ValueKind != JsonValueKind.Null ? DateTime.Parse(tudt.GetString()) : DBNull.Value);

                        await taskCmd.ExecuteNonQueryAsync();
                    }
                }

                migrated++;
                Console.WriteLine($"  + Migrated investor: {investor.GetProperty("name").GetString()} ({investorId})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Error migrating investor {investorId}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"✓ Migration complete! Migrated {migrated} investors");

        // Verify
        var finalUserCount = (int)await checkUsersCmd.ExecuteScalarAsync();
        var finalInvestorCount = (int)await checkInvestorsCmd.ExecuteScalarAsync();
        var taskCountCmd = new SqlCommand("SELECT COUNT(*) FROM InvestorTasks", dbConnection);
        var taskCount = (int)await taskCountCmd.ExecuteScalarAsync();

        Console.WriteLine();
        Console.WriteLine("Final database state:");
        Console.WriteLine($"  Users: {finalUserCount}");
        Console.WriteLine($"  Investors: {finalInvestorCount}");
        Console.WriteLine($"  Tasks: {taskCount}");
    }
}
