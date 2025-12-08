using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Iris.Data;
using Iris.Models;

namespace Scripts;

class Program
{
    static async Task Main(string[] args)
    {
        await RunMigration();
    }

    static async Task RunMigration()
    {
        Console.WriteLine("RaiseTracker Data Migration Tool");
        Console.WriteLine("Migrating data from Azure Blob Storage to SQL Database");
        Console.WriteLine();

        // Read configuration
        var baseDir = AppContext.BaseDirectory;
        var appsettingsPath = Path.Combine(baseDir, "..", "..", "..", "..", "Iris.Api", "appsettings.json");
        if (!File.Exists(appsettingsPath))
        {
            appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Iris.Api", "appsettings.json");
        }

        if (!File.Exists(appsettingsPath))
        {
            Console.Error.WriteLine($"appsettings.json not found. Tried: {appsettingsPath}");
            Environment.Exit(1);
        }

        var appsettingsJson = File.ReadAllText(appsettingsPath);
        var appsettings = JsonSerializer.Deserialize<JsonElement>(appsettingsJson);
        var connectionStrings = appsettings.GetProperty("ConnectionStrings");

        var blobConnectionString = connectionStrings.GetProperty("AzureStorage").GetString();
        var dbConnectionString = connectionStrings.GetProperty("DefaultConnection").GetString();

        if (string.IsNullOrEmpty(blobConnectionString))
        {
            Console.Error.WriteLine("AzureStorage connection string not found in appsettings.json");
            Environment.Exit(1);
        }

        if (string.IsNullOrEmpty(dbConnectionString))
        {
            Console.Error.WriteLine("DefaultConnection not found in appsettings.json");
            Environment.Exit(1);
        }

        const string containerName = "seriesa-data";
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        try
        {
            // Connect to blob storage
            Console.WriteLine("Connecting to Azure Blob Storage...");
            var blobServiceClient = new BlobServiceClient(blobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            if (!await containerClient.ExistsAsync())
            {
                Console.Error.WriteLine($"Container '{containerName}' does not exist in blob storage.");
                Environment.Exit(1);
            }

            Console.WriteLine("✓ Connected to blob storage");

            // Connect to database
            Console.WriteLine("Connecting to SQL Database...");
            var dbContextOptions = new DbContextOptionsBuilder<RaiseTrackerDbContext>()
                .UseSqlServer(dbConnectionString)
                .Options;

            using var context = new RaiseTrackerDbContext(dbContextOptions);
            await context.Database.EnsureCreatedAsync();
            Console.WriteLine("✓ Connected to database");
            Console.WriteLine();

            // Migrate Users
            Console.WriteLine("Migrating users...");
            var usersBlob = containerClient.GetBlobClient("users.json");
            if (await usersBlob.ExistsAsync())
            {
                var usersResponse = await usersBlob.DownloadContentAsync();
                var usersJson = usersResponse.Value.Content.ToString();
                var users = JsonSerializer.Deserialize<List<User>>(usersJson, jsonOptions) ?? new List<User>();

                foreach (var user in users)
                {
                    var existing = await context.Users.FindAsync(user.Id);
                    if (existing == null)
                    {
                        context.Users.Add(user);
                        Console.WriteLine($"  + Added user: {user.Username}");
                    }
                    else
                    {
                        existing.Username = user.Username;
                        existing.DisplayName = user.DisplayName;
                        existing.PasswordHash = user.PasswordHash;
                        existing.IsAdmin = user.IsAdmin;
                        Console.WriteLine($"  ~ Updated user: {user.Username}");
                    }
                }

                await context.SaveChangesAsync();
                Console.WriteLine($"✓ Migrated {users.Count} users");
            }
            else
            {
                Console.WriteLine("  ⚠ users.json not found in blob storage");
            }

            Console.WriteLine();

            // Migrate Investors
            Console.WriteLine("Migrating investors...");
            var indexBlob = containerClient.GetBlobClient("index.json");
            var investorIds = new List<string>();

            if (await indexBlob.ExistsAsync())
            {
                var indexResponse = await indexBlob.DownloadContentAsync();
                var indexJson = indexResponse.Value.Content.ToString();
                var index = JsonSerializer.Deserialize<List<InvestorSummary>>(indexJson, jsonOptions) ?? new List<InvestorSummary>();
                investorIds = index.Select(i => i.Id).ToList();
            }
            else
            {
                // Try to find investor files directly
                Console.WriteLine("  index.json not found, scanning investors folder...");
                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: "investors/"))
                {
                    if (blobItem.Name.EndsWith(".json"))
                    {
                        var id = Path.GetFileNameWithoutExtension(blobItem.Name);
                        investorIds.Add(id);
                    }
                }
            }

            Console.WriteLine($"  Found {investorIds.Count} investors to migrate");

            int migratedCount = 0;
            int errorCount = 0;

            foreach (var investorId in investorIds)
            {
                try
                {
                    var investorBlob = containerClient.GetBlobClient($"investors/{investorId}.json");
                    if (!await investorBlob.ExistsAsync())
                    {
                        Console.WriteLine($"  ⚠ Investor file not found: {investorId}");
                        continue;
                    }

                    var investorResponse = await investorBlob.DownloadContentAsync();
                    var investorJson = investorResponse.Value.Content.ToString();
                    var investor = JsonSerializer.Deserialize<Investor>(investorJson, jsonOptions);

                    if (investor == null)
                    {
                        Console.WriteLine($"  ⚠ Failed to deserialize investor: {investorId}");
                        errorCount++;
                        continue;
                    }

                    // Ensure investor ID matches
                    investor.Id = investorId;

                    // Check if investor exists
                    var existing = await context.Investors
                        .Include(i => i.Tasks)
                        .FirstOrDefaultAsync(i => i.Id == investorId);

                    if (existing == null)
                    {
                        // New investor - add tasks
                        if (investor.Tasks != null && investor.Tasks.Any())
                        {
                            foreach (var task in investor.Tasks)
                            {
                                task.InvestorId = investorId;
                                context.InvestorTasks.Add(task);
                            }
                        }

                        context.Investors.Add(investor);
                        Console.WriteLine($"  + Added investor: {investor.Name} ({investorId})");
                    }
                    else
                    {
                        // Update existing investor
                        existing.Name = investor.Name;
                        existing.MainContact = investor.MainContact;
                        existing.ContactEmail = investor.ContactEmail;
                        existing.ContactPhone = investor.ContactPhone;
                        existing.Category = investor.Category;
                        existing.Stage = investor.Stage;
                        existing.CommitAmount = investor.CommitAmount;
                        existing.Notes = investor.Notes;
                        existing.CreatedBy = investor.CreatedBy;
                        existing.CreatedAt = investor.CreatedAt;
                        existing.UpdatedBy = investor.UpdatedBy;
                        existing.UpdatedAt = investor.UpdatedAt;

                        // Handle tasks
                        var existingTaskIds = existing.Tasks.Select(t => t.Id).ToHashSet();
                        var newTaskIds = investor.Tasks?.Select(t => t.Id).ToHashSet() ?? new HashSet<string>();

                        // Remove deleted tasks
                        var tasksToRemove = existing.Tasks.Where(t => !newTaskIds.Contains(t.Id)).ToList();
                        foreach (var task in tasksToRemove)
                        {
                            context.InvestorTasks.Remove(task);
                        }

                        // Add or update tasks
                        if (investor.Tasks != null)
                        {
                            foreach (var task in investor.Tasks)
                            {
                                var existingTask = existing.Tasks.FirstOrDefault(t => t.Id == task.Id);
                                if (existingTask == null)
                                {
                                    task.InvestorId = investorId;
                                    context.InvestorTasks.Add(task);
                                }
                                else
                                {
                                    existingTask.Description = task.Description;
                                    existingTask.DueDate = task.DueDate;
                                    existingTask.Done = task.Done;
                                    existingTask.CreatedAt = task.CreatedAt;
                                    existingTask.UpdatedAt = task.UpdatedAt;
                                }
                            }
                        }

                        Console.WriteLine($"  ~ Updated investor: {investor.Name} ({investorId})");
                    }

                    migratedCount++;

                    // Save every 10 investors to avoid large transactions
                    if (migratedCount % 10 == 0)
                    {
                        await context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Error migrating investor {investorId}: {ex.Message}");
                    errorCount++;
                }
            }

            // Save remaining changes
            await context.SaveChangesAsync();

            Console.WriteLine();
            Console.WriteLine($"✓ Migration complete!");
            Console.WriteLine($"  Migrated: {migratedCount} investors");
            if (errorCount > 0)
            {
                Console.WriteLine($"  Errors: {errorCount}");
            }

            // Verify migration
            Console.WriteLine();
            Console.WriteLine("Verifying migration...");
            var dbUserCount = await context.Users.CountAsync();
            var dbInvestorCount = await context.Investors.CountAsync();
            var dbTaskCount = await context.InvestorTasks.CountAsync();

            Console.WriteLine($"  Users in database: {dbUserCount}");
            Console.WriteLine($"  Investors in database: {dbInvestorCount}");
            Console.WriteLine($"  Tasks in database: {dbTaskCount}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"✗ Migration failed: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
        Environment.Exit(1);
    }
    }
}
