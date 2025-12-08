namespace Iris.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string newPassword);
    Task<bool> SendMagicLinkEmailAsync(string toEmail, string token, string baseUrl);
}
