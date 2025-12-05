using Iris.Models;

namespace Iris.Services;

public interface IAuthService
{
    Task<User?> ValidateUserAsync(string userId, string password);
    string CreateSessionToken(Session session);
    Session? ValidateSessionToken(string token);
    Task<List<UserSummary>> GetUserSummariesAsync();
    Task<string?> ResetPasswordAsync(string email);
}
