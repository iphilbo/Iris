using Azure;
using Azure.Communication.Email;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Prometheus;

namespace Iris.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private EmailClient? _emailClient;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
        InitializeEmailClient();
    }

    private void InitializeEmailClient()
    {
        // Try both formats: Email:ConnectionString (local) and Email__ConnectionString (Azure App Service)
        var connectionString = _configuration["Email:ConnectionString"]
            ?? _configuration["Email__ConnectionString"];

        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                _emailClient = new EmailClient(connectionString);
                // Success - no need to log
            }
            catch (Exception ex)
            {
                _ = SysProc.SysLogItAsync($"Failed to initialize Azure Communication Services Email client: {ex.Message} | Stack trace: {ex.StackTrace}", "System");
                _emailClient = null;
            }
        }
        else
        {
            _ = SysProc.SysLogItAsync("Azure Communication Services connection string not configured. Checked both 'Email:ConnectionString' and 'Email__ConnectionString'", "System");
        }
    }

    public async Task<bool> SendMagicLinkEmailAsync(string toEmail, string token, string baseUrl)
    {
        if (_emailClient == null)
        {
            _ = SysProc.SysLogItAsync($"Email client not initialized. Skipping email send to {toEmail}", "System");
            return false;
        }

        // Try both formats: Email:FromEmail (local) and Email__FromEmail (Azure App Service)
        var fromEmail = _configuration["Email:FromEmail"]
            ?? _configuration["Email__FromEmail"];

        if (string.IsNullOrEmpty(fromEmail))
        {
            _ = SysProc.SysLogItAsync($"FromEmail not configured. Skipping email send to {toEmail}. Checked both 'Email:FromEmail' and 'Email__FromEmail'", "System");
            return false;
        }

        try
        {
            // Construct magic link URL
            var magicLinkUrl = $"{baseUrl.TrimEnd('/')}/api/validate-magic-link?token={Uri.EscapeDataString(token)}";

            var subject = "Your Magic Link - Iris - RaiseTracker";
            var plainTextBody = $@"Hello,

Click the link below to sign in to the Iris - RaiseTracker:

{magicLinkUrl}

This link will expire in 15 minutes.

If you did not request this link, please ignore this email.

Best regards,
Iris - RaiseTracker Team";

            var htmlBody = $@"<html>
<body>
    <h2>Sign In to Iris - RaiseTracker</h2>
    <p>Hello,</p>
    <p>Click the link below to sign in:</p>
    <p><a href=""{magicLinkUrl}"" style=""background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;"">Sign In</a></p>
    <p>Or copy and paste this URL into your browser:</p>
    <p style=""word-break: break-all;"">{magicLinkUrl}</p>
    <p><small>This link will expire in 15 minutes.</small></p>
    <p>If you did not request this link, please ignore this email.</p>
    <p>Best regards,<br/>Iris - RaiseTracker Team</p>
</body>
</html>";

            var emailContent = new EmailContent(subject)
            {
                PlainText = plainTextBody,
                Html = htmlBody
            };

            var emailMessage = new EmailMessage(
                fromEmail,
                toEmail,
                emailContent
            );

            EmailSendOperation emailSendOperation = await _emailClient.SendAsync(Azure.WaitUntil.Completed, emailMessage);

            if (emailSendOperation.HasValue)
            {
                var status = emailSendOperation.Value.Status;
                if (status == EmailSendStatus.Succeeded)
                {
                    return true;
                }
                else
                {
                    _ = SysProc.SysLogItAsync($"Email send operation completed with status: {status} for {toEmail}", "System");
                    return false;
                }
            }
            else
            {
                _ = SysProc.SysLogItAsync($"Email send operation returned no value for {toEmail}", "System");
                return false;
            }
        }
        catch (RequestFailedException ex)
        {
            var errorDetails = $"Azure Communication Services error sending email to {toEmail}: HTTP Status: {ex.Status}, Error Code: {ex.ErrorCode ?? "N/A"}, Message: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorDetails += $", Inner Exception: {ex.InnerException.Message}";
            }
            _ = SysProc.SysLogItAsync(errorDetails, "System");
            return false;
        }
        catch (Exception ex)
        {
            var errorDetails = $"Failed to send email to {toEmail}: {ex.Message}, Exception type: {ex.GetType().Name}, Stack trace: {ex.StackTrace}";
            if (ex.InnerException != null)
            {
                errorDetails += $", Inner exception: {ex.InnerException.Message}";
            }
            _ = SysProc.SysLogItAsync(errorDetails, "System");
            return false;
        }
    }
}
