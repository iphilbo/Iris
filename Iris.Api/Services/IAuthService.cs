using Iris.Models;

namespace Iris.Services;

public interface IAuthService
{
    string CreateSessionToken(Session session);
    Session? ValidateSessionToken(string token);
    Task<List<UserSummary>> GetUserSummariesAsync();
    Task<string?> GenerateMagicLinkTokenAsync(string email);
    Task<User?> ValidateMagicLinkTokenAsync(string token);
}
