using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Iris.Models;

namespace Iris.Services;

public class AuthService : IAuthService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // In-memory storage for magic link tokens (key: token, value: MagicLinkToken)
    private static readonly Dictionary<string, MagicLinkToken> _magicLinkTokens = new();
    private static readonly object _tokenLock = new();

    public AuthService(IBlobStorageService blobStorage, IConfiguration configuration)
    {
        _blobStorage = blobStorage;
        _configuration = configuration;

        // Clean up expired tokens periodically
        _ = Task.Run(CleanupExpiredTokens);
    }

    public async Task<User?> ValidateUserAsync(string userId, string password)
    {
        var users = await _blobStorage.GetUsersAsync();
        // Normalize email to lowercase for comparison
        var normalizedUserId = userId.ToLowerInvariant();
        var user = users.FirstOrDefault(u =>
            u.Id.Equals(normalizedUserId, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(u.Username) && u.Username.ToLowerInvariant().Equals(normalizedUserId)));

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

    public async Task<string?> ResetPasswordAsync(string email)
    {
        var users = await _blobStorage.GetUsersAsync();
        // Normalize email to lowercase for comparison
        var normalizedEmail = email.ToLowerInvariant();
        var user = users.FirstOrDefault(u =>
            u.Id.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(u.Username) && u.Username.ToLowerInvariant().Equals(normalizedEmail)));

        if (user == null)
        {
            return null;
        }

        // Generate a new temporary password
        var newPassword = GenerateTemporaryPassword();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        // Update user in storage
        await _blobStorage.SaveUsersAsync(users);

        // In a real application, you would send this via email
        // For now, we'll return it (not secure, but functional)
        return newPassword;
    }

    private string GenerateTemporaryPassword()
    {
        // Generate a random 12-character password
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public async Task<string?> GenerateMagicLinkTokenAsync(string email)
    {
        var users = await _blobStorage.GetUsersAsync();
        var normalizedEmail = email.ToLowerInvariant();
        var user = users.FirstOrDefault(u =>
            u.Id.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(u.Username) && u.Username.ToLowerInvariant().Equals(normalizedEmail)));

        if (user == null)
        {
            // Don't reveal if user exists for security
            return null;
        }

        // Generate a secure random token
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "")
            .Substring(0, 43); // Base64 URL-safe token

        var magicLinkToken = new MagicLinkToken
        {
            Token = token,
            UserId = user.Id,
            Email = normalizedEmail,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // Token expires in 15 minutes
            Used = false
        };

        lock (_tokenLock)
        {
            _magicLinkTokens[token] = magicLinkToken;
        }

        return token;
    }

    public async Task<User?> ValidateMagicLinkTokenAsync(string token)
    {
        MagicLinkToken? magicLinkToken;

        lock (_tokenLock)
        {
            if (!_magicLinkTokens.TryGetValue(token, out magicLinkToken))
            {
                return null;
            }

            // Check if token is expired or already used
            if (magicLinkToken.Used || magicLinkToken.ExpiresAt < DateTime.UtcNow)
            {
                _magicLinkTokens.Remove(token);
                return null;
            }

            // Mark token as used
            magicLinkToken.Used = true;
        }

        // Get user from storage
        var users = await _blobStorage.GetUsersAsync();
        var user = users.FirstOrDefault(u => u.Id == magicLinkToken.UserId);

        return user;
    }

    private async Task CleanupExpiredTokens()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(5)); // Run every 5 minutes

            lock (_tokenLock)
            {
                var expiredTokens = _magicLinkTokens
                    .Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow || kvp.Value.Used)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var expiredToken in expiredTokens)
                {
                    _magicLinkTokens.Remove(expiredToken);
                }
            }
        }
    }
}
