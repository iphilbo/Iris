using Azure;
using Iris.Models;

namespace Iris.Services;

public interface IBlobStorageService
{
    Task<List<InvestorSummary>> GetInvestorIndexAsync();
    Task<Investor?> GetInvestorAsync(string id);
    Task<(bool Success, string? ETag)> SaveInvestorAsync(Investor investor, string? ifMatchETag = null);
    Task<bool> DeleteInvestorAsync(string id);
    Task UpdateInvestorIndexAsync(List<InvestorSummary> index);
    Task<List<User>> GetUsersAsync();
    Task SaveUsersAsync(List<User> users);
    Task InitializeAsync();
}
