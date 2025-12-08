using Microsoft.EntityFrameworkCore;
using Iris.Data;
using Iris.Middleware;
using Iris.Models;
using Iris.Services;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Iris;

/// <summary>
/// Standalone Iris application configuration for use as a sub-application.
/// This allows Iris to run on /RaiseTracker subpath with no code overlap.
/// </summary>
public static class IrisApp
{
    /// <summary>
    /// Configures Iris as a standalone sub-application on the specified path base.
    /// </summary>
    public static void ConfigureIris(WebApplicationBuilder builder)
    {
        // Register DbContext
        var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(dbConnectionString))
        {
            builder.Services.AddDbContext<IrisDbContext>(options =>
                options.UseSqlServer(dbConnectionString));
        }

        // Add services (database-backed)
        builder.Services.AddSingleton<IBlobStorageService, DatabaseStorageService>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                }
                else
                {
                    policy.WithOrigins("https://your-app-service.azurewebsites.net")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
            });
        });

        // Configure static files for Iris's wwwroot
        // This will be served from Iris.Api/wwwroot when path base is /RaiseTracker
        builder.WebHost.UseStaticWebAssets();
    }

    /// <summary>
    /// Configures the Iris middleware pipeline and routes.
    /// Call this after app.Build() with UsePathBase("/RaiseTracker") set.
    /// </summary>
    public static async Task SetupIrisPipeline(IApplicationBuilder app)
    {
        // Check if services are configured
        var serviceProvider = app.ApplicationServices;
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("AzureStorage");

        // Configure static files to serve from Iris's wwwroot
        // Static web assets from referenced projects are automatically included in publish output
        var env = serviceProvider.GetRequiredService<IWebHostEnvironment>();
        IFileProvider fileProvider;

        // Try multiple locations for the wwwroot files
        var possiblePaths = new List<string>();

        // 1. Development: relative to current directory (standalone mode)
        var currentDir = Directory.GetCurrentDirectory();
        var devPath = Path.Combine(currentDir, "wwwroot");
        possiblePaths.Add(Path.GetFullPath(devPath));

        // 2. Development: relative to project directory
        var projectPath = Path.Combine(currentDir, "Iris.Api", "wwwroot");
        possiblePaths.Add(Path.GetFullPath(projectPath));

        // 3. Production: in publish output, static web assets are typically in _content/{ProjectName}/
        var assemblyName = typeof(IrisApp).Assembly.GetName().Name ?? "Iris.Api";
        var contentPath = Path.Combine(currentDir, "_content", assemblyName, "wwwroot");
        possiblePaths.Add(Path.GetFullPath(contentPath));

        // 4. Production: directly in the output directory (if copied)
        var outputPath = Path.Combine(currentDir, "wwwroot");
        possiblePaths.Add(Path.GetFullPath(outputPath));

        // Find the first existing path
        string? foundPath = null;
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "raise-tracker.html")))
            {
                foundPath = path;
                break;
            }
        }

        if (foundPath != null)
        {
            fileProvider = new PhysicalFileProvider(foundPath);
        }
        else
        {
            // Last resort: try embedded resources (requires files to be embedded)
            var irisAssembly = typeof(IrisApp).Assembly;
            fileProvider = new ManifestEmbeddedFileProvider(irisAssembly, "wwwroot");
        }

        var defaultFilesOptions = new DefaultFilesOptions
        {
            FileProvider = fileProvider,
            RequestPath = ""
        };
        defaultFilesOptions.DefaultFileNames.Clear();
        defaultFilesOptions.DefaultFileNames.Add("raise-tracker.html");
        app.UseDefaultFiles(defaultFilesOptions);

        var staticFileOptions = new StaticFileOptions
        {
            FileProvider = fileProvider,
            RequestPath = ""
        };
        app.UseStaticFiles(staticFileOptions);

        // Check if database connection is configured
        var dbConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(dbConnectionString))
        {
            // If database is not configured, just serve static files and return
            return;
        }

        // Initialize database storage
        var blobStorage = serviceProvider.GetRequiredService<IBlobStorageService>();
        await blobStorage.InitializeAsync();

        // Middleware
        app.UseCors();
        app.UseRateLimiting();
        app.UseSessionMiddleware();

        // Static files for frontend are already configured above with Iris's wwwroot

        // Enable routing and endpoints
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            // Auth endpoints
            endpoints.MapGet("/api/users", async (IBlobStorageService blobStorage, IAuthService authService, HttpContext context) =>
            {
                var session = GetSession(context, authService);
                if (session != null && session.IsAdmin)
                {
                    // Return full user details for admins
                    var users = await blobStorage.GetUsersAsync();
                    return Results.Ok(users.Select(u => new { id = u.Id, username = u.Username, displayName = u.DisplayName, isAdmin = u.IsAdmin }));
                }
                else
                {
                    // Return summaries for non-admins
                    var users = await authService.GetUserSummariesAsync();
                    return Results.Ok(users);
                }
            });


            endpoints.MapGet("/api/session", (HttpContext context, IAuthService authService) =>
            {
                var cookie = context.Request.Cookies["AuthSession"];
                if (string.IsNullOrEmpty(cookie))
                {
                    return Results.Unauthorized();
                }

                var session = authService.ValidateSessionToken(cookie);
                if (session == null)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new { userId = session.UserId, displayName = session.DisplayName, isAdmin = session.IsAdmin });
            });

            endpoints.MapPost("/api/logout", (HttpContext context) =>
            {
                context.Response.Cookies.Delete("AuthSession");
                return Results.Ok();
            });

            // User management endpoints (admin only)
            endpoints.MapPost("/api/users", async (CreateUserRequest request, IBlobStorageService blobStorage, IAuthService authService, HttpContext context) =>
            {
                var session = GetSession(context, authService);
                if (session == null || !session.IsAdmin)
                {
                    return Results.Unauthorized();
                }

                // Validate email format
                var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");
                if (!emailRegex.IsMatch(request.Username))
                {
                    return Results.BadRequest(new ErrorResponse { Error = "Username must be a valid email address" });
                }

                var users = await blobStorage.GetUsersAsync();
                if (users.Any(u => u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)))
                {
                    return Results.BadRequest(new ErrorResponse { Error = "Email address already exists" });
                }

                var newUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = request.Username.ToLowerInvariant(), // Store email in lowercase
                    DisplayName = request.DisplayName,
                    IsAdmin = request.IsAdmin
                };

                users.Add(newUser);
                await blobStorage.SaveUsersAsync(users);

                return Results.Ok(new { id = newUser.Id, username = newUser.Username, displayName = newUser.DisplayName, isAdmin = newUser.IsAdmin });
            });

            endpoints.MapPut("/api/users/{id}", async (string id, JsonElement body, IBlobStorageService blobStorage, IAuthService authService, HttpContext context) =>
            {
                var session = GetSession(context, authService);
                if (session == null || !session.IsAdmin)
                {
                    return Results.Unauthorized();
                }

                var users = await blobStorage.GetUsersAsync();
                var user = users.FirstOrDefault(u => u.Id == id);
                if (user == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "User not found" });
                }

                var request = JsonSerializer.Deserialize<UpdateUserRequest>(body.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request == null)
                {
                    return Results.BadRequest(new ErrorResponse { Error = "Invalid request" });
                }

                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    user.DisplayName = request.DisplayName;
                }

                if (request.IsAdmin.HasValue)
                {
                    user.IsAdmin = request.IsAdmin.Value;
                }

                await blobStorage.SaveUsersAsync(users);

                return Results.Ok(new { id = user.Id, username = user.Username, displayName = user.DisplayName, isAdmin = user.IsAdmin });
            });

            endpoints.MapDelete("/api/users/{id}", async (string id, IBlobStorageService blobStorage, IAuthService authService, HttpContext context) =>
            {
                var session = GetSession(context, authService);
                if (session == null || !session.IsAdmin)
                {
                    return Results.Unauthorized();
                }

                if (session.UserId == id)
                {
                    return Results.BadRequest(new ErrorResponse { Error = "Cannot delete your own account" });
                }

                var users = await blobStorage.GetUsersAsync();
                var user = users.FirstOrDefault(u => u.Id == id);
                if (user == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "User not found" });
                }

                users.Remove(user);
                await blobStorage.SaveUsersAsync(users);

                return Results.Ok();
            });

            // Investor endpoints
            endpoints.MapGet("/api/investors", async (IBlobStorageService blobStorage) =>
            {
                var index = await blobStorage.GetInvestorIndexAsync();
                return Results.Ok(index);
            });

            endpoints.MapGet("/api/investors/{id}", async (string id, IBlobStorageService blobStorage) =>
            {
                var investor = await blobStorage.GetInvestorAsync(id);
                if (investor == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "Investor not found" });
                }
                return Results.Ok(investor);
            });

            endpoints.MapPost("/api/investors", async (CreateInvestorRequest request, IBlobStorageService blobStorage, HttpContext context) =>
            {
                var userId = context.Items["UserId"]?.ToString() ?? "unknown";

                var investor = new Investor
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = request.Name,
                    MainContact = request.MainContact,
                    ContactEmail = request.ContactEmail,
                    ContactPhone = request.ContactPhone,
                    Category = request.Category,
                    Stage = request.Stage,
                    Status = request.Status ?? "Active",
                    CommitAmount = request.CommitAmount,
                    Notes = request.Notes,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow,
                    Tasks = new List<InvestorTask>()
                };

                var (success, etag) = await blobStorage.SaveInvestorAsync(investor);
                if (!success)
                {
                    return Results.Conflict(new ErrorResponse { Error = "Failed to save investor" });
                }

                // Update index
                var index = await blobStorage.GetInvestorIndexAsync();
                index.Add(new InvestorSummary
                {
                    Id = investor.Id,
                    Name = investor.Name,
                    Stage = investor.Stage,
                    Category = investor.Category,
                    Status = investor.Status,
                    CommitAmount = investor.CommitAmount,
                    UpdatedAt = investor.UpdatedAt
                });
                await blobStorage.UpdateInvestorIndexAsync(index);

                return Results.Created($"/api/investors/{investor.Id}", investor);
            });

            endpoints.MapPut("/api/investors/{id}", async (string id, JsonElement body, IBlobStorageService blobStorage, HttpContext context) =>
            {
                var userId = context.Items["UserId"]?.ToString() ?? "unknown";

                var existing = await blobStorage.GetInvestorAsync(id);
                if (existing == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "Investor not found" });
                }

                // Apply updates from request body
                if (body.TryGetProperty("name", out var nameProp)) existing.Name = nameProp.GetString() ?? existing.Name;
                if (body.TryGetProperty("mainContact", out var mainContactProp)) existing.MainContact = mainContactProp.GetString();
                if (body.TryGetProperty("contactEmail", out var emailProp)) existing.ContactEmail = emailProp.GetString();
                if (body.TryGetProperty("contactPhone", out var phoneProp)) existing.ContactPhone = phoneProp.GetString();
                if (body.TryGetProperty("category", out var categoryProp)) existing.Category = categoryProp.GetString() ?? existing.Category;
                if (body.TryGetProperty("stage", out var stageProp)) existing.Stage = stageProp.GetString() ?? existing.Stage;
                if (body.TryGetProperty("status", out var statusProp)) existing.Status = statusProp.GetString() ?? existing.Status;
                if (body.TryGetProperty("commitAmount", out var amountProp)) existing.CommitAmount = amountProp.GetDecimal();
                if (body.TryGetProperty("notes", out var notesProp)) existing.Notes = notesProp.GetString();

                existing.UpdatedBy = userId;
                existing.UpdatedAt = DateTime.UtcNow;

                var (success, etag) = await blobStorage.SaveInvestorAsync(existing);
                if (!success)
                {
                    return Results.Conflict(new ErrorResponse { Error = "Data changed, please reload", Code = "ETAG_MISMATCH" });
                }

                // Update index
                var index = await blobStorage.GetInvestorIndexAsync();
                var indexItem = index.FirstOrDefault(i => i.Id == id);
                if (indexItem != null)
                {
                    indexItem.Name = existing.Name;
                    indexItem.Stage = existing.Stage;
                    indexItem.Category = existing.Category;
                    indexItem.Status = existing.Status;
                    indexItem.CommitAmount = existing.CommitAmount;
                    indexItem.UpdatedAt = existing.UpdatedAt;
                    await blobStorage.UpdateInvestorIndexAsync(index);
                }

                return Results.Ok(existing);
            });

            endpoints.MapDelete("/api/investors/{id}", async (string id, IBlobStorageService blobStorage) =>
            {
                var deleted = await blobStorage.DeleteInvestorAsync(id);
                if (!deleted)
                {
                    return Results.NotFound(new ErrorResponse { Error = "Investor not found" });
                }

                // Update index
                var index = await blobStorage.GetInvestorIndexAsync();
                index.RemoveAll(i => i.Id == id);
                await blobStorage.UpdateInvestorIndexAsync(index);

                return Results.NoContent();
            });

            // Task endpoints
            endpoints.MapPost("/api/investors/{id}/tasks", async (string id, CreateTaskRequest request, IBlobStorageService blobStorage) =>
            {
                var investor = await blobStorage.GetInvestorAsync(id);
                if (investor == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "Investor not found" });
                }

                var task = new InvestorTask
                {
                    Id = Guid.NewGuid().ToString(),
                    InvestorId = id,
                    Description = request.Description,
                    DueDate = request.DueDate,
                    Done = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                investor.Tasks.Add(task);
                investor.UpdatedAt = DateTime.UtcNow;

                var (success, _) = await blobStorage.SaveInvestorAsync(investor);
                if (!success)
                {
                    return Results.Conflict(new ErrorResponse { Error = "Failed to save task" });
                }

                return Results.Ok(investor);
            });

            endpoints.MapPut("/api/investors/{id}/tasks/{taskId}", async (string id, string taskId, JsonElement body, IBlobStorageService blobStorage) =>
            {
                var investor = await blobStorage.GetInvestorAsync(id);
                if (investor == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "Investor not found" });
                }

                var task = investor.Tasks.FirstOrDefault(t => t.Id == taskId);
                if (task == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "Task not found" });
                }

                if (body.TryGetProperty("description", out var descProp)) task.Description = descProp.GetString() ?? task.Description;
                if (body.TryGetProperty("dueDate", out var dueProp)) task.DueDate = dueProp.GetString() ?? task.DueDate;
                if (body.TryGetProperty("done", out var doneProp)) task.Done = doneProp.GetBoolean();

                task.UpdatedAt = DateTime.UtcNow;
                investor.UpdatedAt = DateTime.UtcNow;

                var (success, _) = await blobStorage.SaveInvestorAsync(investor);
                if (!success)
                {
                    return Results.Conflict(new ErrorResponse { Error = "Data changed, please reload", Code = "ETAG_MISMATCH" });
                }

                return Results.Ok(investor);
            });

            endpoints.MapDelete("/api/investors/{id}/tasks/{taskId}", async (string id, string taskId, IBlobStorageService blobStorage) =>
            {
                var investor = await blobStorage.GetInvestorAsync(id);
                if (investor == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "Investor not found" });
                }

                var task = investor.Tasks.FirstOrDefault(t => t.Id == taskId);
                if (task == null)
                {
                    return Results.NotFound(new ErrorResponse { Error = "Task not found" });
                }

                investor.Tasks.Remove(task);
                investor.UpdatedAt = DateTime.UtcNow;

                var (success, _) = await blobStorage.SaveInvestorAsync(investor);
                if (!success)
                {
                    return Results.Conflict(new ErrorResponse { Error = "Failed to delete task" });
                }

                return Results.Ok(investor);
            });
        });
    }

    private static Session? GetSession(HttpContext context, IAuthService authService)
    {
        var cookie = context.Request.Cookies["AuthSession"];
        if (string.IsNullOrEmpty(cookie))
        {
            return null;
        }

        return authService.ValidateSessionToken(cookie);
    }
}
