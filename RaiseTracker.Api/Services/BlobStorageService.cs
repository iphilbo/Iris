using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RaiseTracker.Api.Models;

namespace RaiseTracker.Api.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName = "seriesa-data";
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public BlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AzureStorage")
            ?? throw new InvalidOperationException("AzureStorage connection string is not configured");
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task InitializeAsync()
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

        // Initialize users.json if it doesn't exist
        var usersBlob = containerClient.GetBlobClient("users.json");
        if (!await usersBlob.ExistsAsync())
        {
            var initialUsers = new List<User>
            {
                new User
                {
                    Id = "user-1",
                    Username = "phil",
                    DisplayName = "Phil",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("General123"),
                    IsAdmin = true
                }
            };
            await SaveUsersAsync(initialUsers);
        }

        // Initialize index.json if it doesn't exist
        var indexBlob = containerClient.GetBlobClient("index.json");
        if (!await indexBlob.ExistsAsync())
        {
            await UpdateInvestorIndexAsync(new List<InvestorSummary>());
        }
    }

    public async Task<List<InvestorSummary>> GetInvestorIndexAsync()
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient("index.json");

        if (!await blobClient.ExistsAsync())
        {
            return new List<InvestorSummary>();
        }

        var response = await blobClient.DownloadContentAsync();
        var json = response.Value.Content.ToString();
        return JsonSerializer.Deserialize<List<InvestorSummary>>(json, _jsonOptions) ?? new List<InvestorSummary>();
    }

    public async Task<Investor?> GetInvestorAsync(string id)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient($"investors/{id}.json");

        if (!await blobClient.ExistsAsync())
        {
            return null;
        }

        var response = await blobClient.DownloadContentAsync();
        var json = response.Value.Content.ToString();
        return JsonSerializer.Deserialize<Investor>(json, _jsonOptions);
    }

    public async Task<(bool Success, string? ETag)> SaveInvestorAsync(Investor investor, string? ifMatchETag = null)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient($"investors/{investor.Id}.json");

        var json = JsonSerializer.Serialize(investor, _jsonOptions);
        var content = Encoding.UTF8.GetBytes(json);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
        };

        if (ifMatchETag != null)
        {
            uploadOptions.Conditions = new BlobRequestConditions { IfMatch = new ETag(ifMatchETag) };
        }

        try
        {
            var response = await blobClient.UploadAsync(new BinaryData(content), uploadOptions);
            return (true, response.Value.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 412) // Precondition Failed
        {
            return (false, null);
        }
    }

    public async Task<bool> DeleteInvestorAsync(string id)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient($"investors/{id}.json");

        if (!await blobClient.ExistsAsync())
        {
            return false;
        }

        await blobClient.DeleteAsync();
        return true;
    }

    public async Task UpdateInvestorIndexAsync(List<InvestorSummary> index)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient("index.json");

        var json = JsonSerializer.Serialize(index, _jsonOptions);
        var content = Encoding.UTF8.GetBytes(json);

        await blobClient.UploadAsync(new BinaryData(content), overwrite: true);
    }

    public async Task<List<User>> GetUsersAsync()
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient("users.json");

        if (!await blobClient.ExistsAsync())
        {
            return new List<User>();
        }

        var response = await blobClient.DownloadContentAsync();
        var json = response.Value.Content.ToString();
        return JsonSerializer.Deserialize<List<User>>(json, _jsonOptions) ?? new List<User>();
    }

    public async Task SaveUsersAsync(List<User> users)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient("users.json");

        var json = JsonSerializer.Serialize(users, _jsonOptions);
        var content = Encoding.UTF8.GetBytes(json);

        await blobClient.UploadAsync(new BinaryData(content), overwrite: true);
    }
}
