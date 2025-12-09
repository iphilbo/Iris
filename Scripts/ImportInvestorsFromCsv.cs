using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Scripts;

class ImportInvestorsFromCsv
{
    static void Main(string[] args)
    {
        Console.WriteLine("CSV Investor Import Tool");
        Console.WriteLine("=======================\n");

        // Get CSV file path
        var csvPath = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "..", "Investor Contact List.csv");

        if (!File.Exists(csvPath))
        {
            Console.Error.WriteLine($"CSV file not found at: {csvPath}");
            Console.Error.WriteLine("Usage: ImportInvestorsFromCsv.exe [path-to-csv-file]");
            Environment.Exit(1);
        }

        Console.WriteLine($"Reading CSV from: {csvPath}");

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

        // Use JP user ID
        const string jpUserId = "a61aad17-0ca6-4e9c-9054-eff652a438f2";
        Console.WriteLine($"Using CreatedBy: {jpUserId} (JP)\n");

        // Parse CSV
        var investors = ParseCsv(csvPath);
        Console.WriteLine($"Found {investors.Count} investors in CSV\n");

        if (investors.Count == 0)
        {
            Console.WriteLine("No investors found in CSV. Exiting.");
            return;
        }

        Console.WriteLine($"Importing all {investors.Count} investors...\n");

        // Import to database
        try
        {
            using var connection = new SqlConnection(dbConnectionString);
            connection.Open();

            int successCount = 0;
            int errorCount = 0;

            foreach (var investor in investors)
            {
                try
                {
                    InsertInvestor(connection, investor, jpUserId);
                    successCount++;
                    if (successCount % 10 == 0)
                    {
                        Console.Write(".");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Console.WriteLine($"\nError importing {investor.Company}: {ex.Message}");
                }
            }

            Console.WriteLine($"\n\nImport complete!");
            Console.WriteLine($"  Successfully imported: {successCount}");
            Console.WriteLine($"  Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error connecting to database: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    static List<CsvInvestor> ParseCsv(string csvPath)
    {
        var investors = new List<CsvInvestor>();
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);

        if (lines.Length < 2)
        {
            return investors;
        }

        // Skip header row (line 0)
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue; // Skip empty rows
            }

            var fields = ParseCsvLine(line);
            if (fields.Count < 8)
            {
                Console.WriteLine($"Warning: Skipping row {i + 1} - insufficient columns");
                continue;
            }

            // Check if company name is empty
            if (string.IsNullOrWhiteSpace(fields[1]))
            {
                continue; // Skip rows without company name
            }

            var investor = new CsvInvestor
            {
                InvestorType = fields[0]?.Trim() ?? "",
                Company = fields[1]?.Trim() ?? "",
                CallResponsibility = fields[2]?.Trim() ?? "",
                PrimaryInvestorType = fields[3]?.Trim() ?? "",
                PreferredInvestmentTypes = fields[4]?.Trim() ?? "",
                PrimaryContact = fields[5]?.Trim() ?? "",
                PrimaryContactPhone = fields[6]?.Trim() ?? "",
                PrimaryContactEmail = fields[7]?.Trim() ?? ""
            };

            investors.Add(investor);
        }

        return investors;
    }

    static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentField.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // End of field
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        // Add last field
        fields.Add(currentField.ToString());

        return fields;
    }

    static void InsertInvestor(SqlConnection connection, CsvInvestor csvInvestor, string jpUserId)
    {
        var investorId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        // Format Notes field
        var notes = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(csvInvestor.InvestorType))
        {
            notes.AppendLine($"Investor Type: {csvInvestor.InvestorType}");
        }
        if (!string.IsNullOrWhiteSpace(csvInvestor.PrimaryInvestorType))
        {
            notes.AppendLine($"Primary Investor Type: {csvInvestor.PrimaryInvestorType}");
        }
        if (!string.IsNullOrWhiteSpace(csvInvestor.PreferredInvestmentTypes))
        {
            notes.AppendLine($"Preferred Investment Types: {csvInvestor.PreferredInvestmentTypes}");
        }
        var notesText = notes.ToString().Trim();

        // Map CSV fields to investor model
        var category = csvInvestor.InvestorType; // "Strategic" or "Financial" - added as new category values
        var stage = "target"; // Default to target stage
        var status = ""; // Default to blank status

        var cmd = new SqlCommand(@"
            INSERT INTO Investors
                (Id, Name, MainContact, ContactEmail, ContactPhone, Category, Stage, Status, Owner, CommitAmount, Notes, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
            VALUES
                (@Id, @Name, @MainContact, @ContactEmail, @ContactPhone, @Category, @Stage, @Status, @Owner, @CommitAmount, @Notes, @CreatedBy, @CreatedAt, @UpdatedBy, @UpdatedAt)",
            connection);

        cmd.Parameters.AddWithValue("@Id", investorId);
        cmd.Parameters.AddWithValue("@Name", csvInvestor.Company);
        cmd.Parameters.AddWithValue("@MainContact", (object?)csvInvestor.PrimaryContact ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContactEmail", (object?)csvInvestor.PrimaryContactEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContactPhone", (object?)csvInvestor.PrimaryContactPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Category", category);
        cmd.Parameters.AddWithValue("@Stage", stage);
        cmd.Parameters.AddWithValue("@Status", status ?? string.Empty);
        cmd.Parameters.AddWithValue("@Owner", (object?)csvInvestor.CallResponsibility ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CommitAmount", DBNull.Value);
        cmd.Parameters.AddWithValue("@Notes", (object?)notesText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", jpUserId);
        cmd.Parameters.AddWithValue("@CreatedAt", now);
        cmd.Parameters.AddWithValue("@UpdatedBy", jpUserId);
        cmd.Parameters.AddWithValue("@UpdatedAt", now);

        cmd.ExecuteNonQuery();
    }

    class CsvInvestor
    {
        public string InvestorType { get; set; } = "";
        public string Company { get; set; } = "";
        public string CallResponsibility { get; set; } = "";
        public string PrimaryInvestorType { get; set; } = "";
        public string PreferredInvestmentTypes { get; set; } = "";
        public string PrimaryContact { get; set; } = "";
        public string PrimaryContactPhone { get; set; } = "";
        public string PrimaryContactEmail { get; set; } = "";
    }
}
