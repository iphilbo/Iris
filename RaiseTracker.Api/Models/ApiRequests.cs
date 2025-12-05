namespace Iris.Models;

public class LoginRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class CreateInvestorRequest
{
    public string Name { get; set; } = string.Empty;
    public string? MainContact { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string? Status { get; set; }
    public decimal? CommitAmount { get; set; }
    public string? Notes { get; set; }
}

public class CreateTaskRequest
{
    public string Description { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty; // YYYY-MM-DD
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}

public class UpdateUserRequest
{
    public string? DisplayName { get; set; }
    public string? Password { get; set; }
    public bool? IsAdmin { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Code { get; set; }
}
