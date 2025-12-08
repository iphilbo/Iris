namespace Iris.Services;

public interface IEmailService
{
    Task<bool> SendMagicLinkEmailAsync(string toEmail, string token, string baseUrl);
}
