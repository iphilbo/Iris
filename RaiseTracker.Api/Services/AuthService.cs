using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RaiseTracker.Api.Models;

namespace RaiseTracker.Api.Services;

public class AuthService : IAuthService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AuthService(IBlobStorageService blobStorage, IConfiguration configuration)
    {
        _blobStorage = blobStorage;
        _configuration = configuration;
    }

    public async Task<User?> ValidateUserAsync(string userId, string password)
    {
        var users = await _blobStorage.GetUsersAsync();
        var user = users.FirstOrDefault(u =>
            u.Id.Equals(userId, StringComparison.OrdinalIgnoreCase) ||
            u.Username.Equals(userId, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            return null;
        }

        if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return user;
        }

        return null;
    }

    public string CreateSessionToken(Session session)
    {
        var json = JsonSerializer.Serialize(session, _jsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        // Simple HMAC-based signing (lean/simple approach)
        var key = _configuration["SessionSigningKey"] ?? "default-key-change-in-production";
        var keyBytes = Encoding.UTF8.GetBytes(key);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(jsonBytes);
        var signature = Convert.ToBase64String(hash);

        var payload = Convert.ToBase64String(jsonBytes);
        return $"{payload}.{signature}";
    }

    public Session? ValidateSessionToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2)
            {
                return null;
            }

            var payload = parts[0];
            var signature = parts[1];

            var jsonBytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(jsonBytes);

            // Verify signature
            var key = _configuration["SessionSigningKey"] ?? "default-key-change-in-production";
            var keyBytes = Encoding.UTF8.GetBytes(key);

            using var hmac = new HMACSHA256(keyBytes);
            var computedHash = hmac.ComputeHash(jsonBytes);
            var computedSignature = Convert.ToBase64String(computedHash);

            if (computedSignature != signature)
            {
                return null;
            }

            var session = JsonSerializer.Deserialize<Session>(json, _jsonOptions);

            // Check expiry
            if (session == null || session.ExpiresAt < DateTime.UtcNow)
            {
                return null;
            }

            return session;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<UserSummary>> GetUserSummariesAsync()
    {
        var users = await _blobStorage.GetUsersAsync();
        return users.Select(u => new UserSummary
        {
            Id = u.Id,
            DisplayName = u.DisplayName
        }).ToList();
    }
}
