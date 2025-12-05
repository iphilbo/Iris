using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaiseTracker.Api.Data;
using RaiseTracker.Api.Models;

namespace RaiseTracker.Api.Services;

public class DatabaseStorageService : IBlobStorageService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DatabaseStorageService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    private RaiseTrackerDbContext CreateContext()
    {
        var scope = _serviceScopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<RaiseTrackerDbContext>();
    }

    public async Task InitializeAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RaiseTrackerDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Check if admin user exists, if not create it
        var adminExists = await context.Users.AnyAsync(u => u.Username == "phil");
        if (!adminExists)
        {
            var adminUser = new User
            {
                Id = "user-1",
                Username = "phil",
                DisplayName = "Phil",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("General123"),
                IsAdmin = true
            };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<InvestorSummary>> GetInvestorIndexAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RaiseTrackerDbContext>();

        return await context.Investors
            .Select(i => new InvestorSummary
            {
                Id = i.Id,
                Name = i.Name,
                Stage = i.Stage,
                Category = i.Category,
                Status = i.Status,
                CommitAmount = i.CommitAmount,
                UpdatedAt = i.UpdatedAt
            })
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Investor?> GetInvestorAsync(string id)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RaiseTrackerDbContext>();

        var investor = await context.Investors
            .Include(i => i.Tasks)
            .FirstOrDefaultAsync(i => i.Id == id);

        return investor;
    }

    public async Task<(bool Success, string? ETag)> SaveInvestorAsync(Investor investor, string? ifMatchETag = null)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RaiseTrackerDbContext>();

        try
        {
            var existing = await context.Investors
                .Include(i => i.Tasks)
                .FirstOrDefaultAsync(i => i.Id == investor.Id);

            if (existing == null)
            {
                // New investor
                investor.Tasks ??= new List<InvestorTask>();
                context.Investors.Add(investor);

                // Add tasks
                foreach (var task in investor.Tasks)
                {
                    context.InvestorTasks.Add(task);
                }
            }
            else
            {
                // Update existing investor
                // Check optimistic concurrency if ETag provided
                if (ifMatchETag != null)
                {
                    var expectedRowVersion = Convert.FromBase64String(ifMatchETag);
                    if (!existing.RowVersion?.SequenceEqual(expectedRowVersion) ?? false)
                    {
                        return (false, null); // Concurrency conflict
                    }
                }

                // Update properties
                existing.Name = investor.Name;
                existing.MainContact = investor.MainContact;
                existing.ContactEmail = investor.ContactEmail;
                existing.ContactPhone = investor.ContactPhone;
                existing.Category = investor.Category;
                existing.Stage = investor.Stage;
                existing.Status = investor.Status;
                existing.CommitAmount = investor.CommitAmount;
                existing.Notes = investor.Notes;
                existing.UpdatedBy = investor.UpdatedBy;
                existing.UpdatedAt = investor.UpdatedAt;

                // Handle tasks - remove deleted ones, add/update existing ones
                var existingTaskIds = existing.Tasks.Select(t => t.Id).ToHashSet();
                var newTaskIds = investor.Tasks?.Select(t => t.Id).ToHashSet() ?? new HashSet<string>();

                // Remove tasks that are no longer in the investor
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
                            // New task
                            task.InvestorId = investor.Id;
                            context.InvestorTasks.Add(task);
                            existing.Tasks.Add(task);
                        }
                        else
                        {
                            // Update existing task
                            existingTask.Description = task.Description;
                            existingTask.DueDate = task.DueDate;
                            existingTask.Done = task.Done;
                            existingTask.UpdatedAt = task.UpdatedAt;
                        }
                    }
                }
            }

            await context.SaveChangesAsync();

            // Reload to get updated RowVersion
            var saved = await context.Investors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == investor.Id);

            var etag = saved?.RowVersion != null ? Convert.ToBase64String(saved.RowVersion) : null;
            return (true, etag);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, null); // Concurrency conflict
        }
    }

    public async Task<bool> DeleteInvestorAsync(string id)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RaiseTrackerDbContext>();

        var investor = await context.Investors
            .Include(i => i.Tasks)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (investor == null)
        {
            return false;
        }

        // Tasks will be deleted automatically due to CASCADE DELETE
        context.Investors.Remove(investor);
        await context.SaveChangesAsync();

        return true;
    }

    public async Task UpdateInvestorIndexAsync(List<InvestorSummary> index)
    {
        // No-op: Index is computed from Investors table
        // This method is kept for interface compatibility
        await Task.CompletedTask;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RaiseTrackerDbContext>();

        return await context.Users.ToListAsync();
    }

    public async Task SaveUsersAsync(List<User> users)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RaiseTrackerDbContext>();

        // Get existing users
        var existingUsers = await context.Users.ToListAsync();
        var existingUserIds = existingUsers.Select(u => u.Id).ToHashSet();
        var newUserIds = users.Select(u => u.Id).ToHashSet();

        // Remove users that are no longer in the list
        var usersToRemove = existingUsers.Where(u => !newUserIds.Contains(u.Id)).ToList();
        foreach (var user in usersToRemove)
        {
            context.Users.Remove(user);
        }

        // Add or update users
        foreach (var user in users)
        {
            var existing = existingUsers.FirstOrDefault(u => u.Id == user.Id);
            if (existing == null)
            {
                context.Users.Add(user);
            }
            else
            {
                existing.Username = user.Username;
                existing.DisplayName = user.DisplayName;
                existing.PasswordHash = user.PasswordHash;
                existing.IsAdmin = user.IsAdmin;
            }
        }

        await context.SaveChangesAsync();
    }
}
