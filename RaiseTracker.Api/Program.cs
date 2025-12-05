using Microsoft.EntityFrameworkCore;
using Iris.Data;
using Iris.Middleware;
using Iris.Models;
using Iris.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services
// Register DbContext
builder.Services.AddDbContext<IrisDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register storage service (database-backed)
builder.Services.AddSingleton<IBlobStorageService, DatabaseStorageService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
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

var app = builder.Build();

// Initialize blob storage
var blobStorage = app.Services.GetRequiredService<IBlobStorageService>();
await blobStorage.InitializeAsync();

// Middleware
app.UseCors();
app.UseRateLimiting();
app.UseSessionMiddleware();

// Static files for frontend
var defaultFilesOptions = new DefaultFilesOptions();
defaultFilesOptions.DefaultFileNames.Clear();
defaultFilesOptions.DefaultFileNames.Add("raise-tracker.html");
app.UseDefaultFiles(defaultFilesOptions);
app.UseStaticFiles();

// API Endpoints

// Auth endpoints
app.MapGet("/api/users", async (IBlobStorageService blobStorage, IAuthService authService, HttpContext context) =>
{
    var cookie = context.Request.Cookies["AuthSession"];
    Session? session = null;
    if (!string.IsNullOrEmpty(cookie))
    {
        session = authService.ValidateSessionToken(cookie);
    }

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

app.MapPost("/api/login", async (LoginRequest request, IAuthService authService, HttpContext context) =>
{
    var user = await authService.ValidateUserAsync(request.UserId, request.Password);

    if (user == null)
    {
        return Results.Unauthorized();
    }

    // Clear rate limiting on successful login
    if (context.Items.TryGetValue("RateLimitKey", out var keyObj) && keyObj is string key)
    {
        RateLimitingMiddleware.ClearAttempts(key);
    }

    var session = new Session
    {
        UserId = user.Id,
        DisplayName = user.DisplayName,
        IsAdmin = user.IsAdmin,
        IssuedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7)
    };

    var token = authService.CreateSessionToken(session);

    context.Response.Cookies.Append("AuthSession", token, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = session.ExpiresAt
    });

    return Results.Ok(new { userId = user.Id, displayName = user.DisplayName, isAdmin = user.IsAdmin });
});

app.MapPost("/api/forgot-password", async (ForgotPasswordRequest request, IAuthService authService, IBlobStorageService blobStorage, IEmailService emailService) =>
{
    try
    {
        // Debug: Check if user exists
        var allUsers = await blobStorage.GetUsersAsync();
        var normalizedEmail = request.Email.ToLowerInvariant();
        var foundUser = allUsers.FirstOrDefault(u =>
            u.Id.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(u.Username) && u.Username.ToLowerInvariant().Equals(normalizedEmail)));

        if (foundUser == null)
        {
            // Return generic message for security, but log for debugging
            Console.WriteLine($"Password reset requested for email: {request.Email}, but user not found. Available users: {string.Join(", ", allUsers.Select(u => u.Username))}");
            return Results.Ok(new { message = "If an account with that email exists, a password reset email has been sent." });
        }

        var newPassword = await authService.ResetPasswordAsync(request.Email);

        if (newPassword == null)
        {
            return Results.Ok(new { message = "If an account with that email exists, a password reset email has been sent." });
        }

        // Send password via email
        var emailSent = await emailService.SendPasswordResetEmailAsync(request.Email, newPassword);

        if (emailSent)
        {
            return Results.Ok(new { message = "A password reset email has been sent to your email address. Please check your inbox." });
        }
        else
        {
            // Email not configured or failed - return password in response as fallback (not ideal, but functional)
            Console.WriteLine($"Warning: Email sending failed or not configured. Returning password in response for {request.Email}");
            return Results.Ok(new { message = $"Email sending is not configured. Your new temporary password is: {newPassword}. Please change it after logging in." });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in forgot-password: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Results.Problem("An error occurred processing your request.");
    }
});

app.MapGet("/api/session", (HttpContext context, IAuthService authService) =>
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

app.MapPost("/api/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete("AuthSession");
    return Results.Ok();
});

// User management endpoints (admin only)
app.MapPost("/api/users", async (CreateUserRequest request, IBlobStorageService blobStorage, IAuthService authService, HttpContext context) =>
{
    var cookie = context.Request.Cookies["AuthSession"];
    if (string.IsNullOrEmpty(cookie))
    {
        return Results.Unauthorized();
    }

    var session = authService.ValidateSessionToken(cookie);
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
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        IsAdmin = request.IsAdmin
    };

    users.Add(newUser);
    await blobStorage.SaveUsersAsync(users);

    return Results.Ok(new { id = newUser.Id, username = newUser.Username, displayName = newUser.DisplayName, isAdmin = newUser.IsAdmin });
});

app.MapPut("/api/users/{id}", async (string id, JsonElement body, IBlobStorageService blobStorage, IAuthService authService, HttpContext context) =>
{
    var cookie = context.Request.Cookies["AuthSession"];
    if (string.IsNullOrEmpty(cookie))
    {
        return Results.Unauthorized();
    }

    var session = authService.ValidateSessionToken(cookie);
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

    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
    }

    if (request.IsAdmin.HasValue)
    {
        user.IsAdmin = request.IsAdmin.Value;
    }

    await blobStorage.SaveUsersAsync(users);

    return Results.Ok(new { id = user.Id, username = user.Username, displayName = user.DisplayName, isAdmin = user.IsAdmin });
});

app.MapDelete("/api/users/{id}", async (string id, IBlobStorageService blobStorage, IAuthService authService, HttpContext context) =>
{
    var cookie = context.Request.Cookies["AuthSession"];
    if (string.IsNullOrEmpty(cookie))
    {
        return Results.Unauthorized();
    }

    var session = authService.ValidateSessionToken(cookie);
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
app.MapGet("/api/investors", async (IBlobStorageService blobStorage) =>
{
    var index = await blobStorage.GetInvestorIndexAsync();
    return Results.Ok(index);
});

app.MapGet("/api/investors/{id}", async (string id, IBlobStorageService blobStorage) =>
{
    var investor = await blobStorage.GetInvestorAsync(id);
    if (investor == null)
    {
        return Results.NotFound(new ErrorResponse { Error = "Investor not found" });
    }
    return Results.Ok(investor);
});

app.MapPost("/api/investors", async (CreateInvestorRequest request, IBlobStorageService blobStorage, HttpContext context) =>
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

app.MapPut("/api/investors/{id}", async (string id, JsonElement body, IBlobStorageService blobStorage, HttpContext context) =>
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

app.MapDelete("/api/investors/{id}", async (string id, IBlobStorageService blobStorage) =>
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
app.MapPost("/api/investors/{id}/tasks", async (string id, CreateTaskRequest request, IBlobStorageService blobStorage) =>
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

app.MapPut("/api/investors/{id}/tasks/{taskId}", async (string id, string taskId, JsonElement body, IBlobStorageService blobStorage) =>
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

app.MapDelete("/api/investors/{id}/tasks/{taskId}", async (string id, string taskId, IBlobStorageService blobStorage) =>
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

app.Run();
