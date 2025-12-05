using RaiseTracker.Api.Models;

namespace RaiseTracker.Api.Services;

public interface IAuthService
{
    Task<User?> ValidateUserAsync(string userId, string password);
    string CreateSessionToken(Session session);
    Session? ValidateSessionToken(string token);
    Task<List<UserSummary>> GetUserSummariesAsync();
}
