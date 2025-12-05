using Azure;
using Azure.Communication.Email;
using Azure.Core;
using Microsoft.Extensions.Configuration;

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
        var connectionString = _configuration["Email:ConnectionString"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                _emailClient = new EmailClient(connectionString);
                Console.WriteLine("Azure Communication Services Email client initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize Azure Communication Services Email client: {ex.Message}");
                _emailClient = null;
            }
        }
        else
        {
            Console.WriteLine("Azure Communication Services connection string not configured");
        }
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string newPassword)
    {
        if (_emailClient == null)
        {
            Console.WriteLine($"Email client not initialized. Skipping email send to {toEmail}");
            return false;
        }

        var fromEmail = _configuration["Email:FromEmail"];
        if (string.IsNullOrEmpty(fromEmail))
        {
            Console.WriteLine($"FromEmail not configured. Skipping email send to {toEmail}");
            return false;
        }

        try
        {
            var subject = "Password Reset - Series A Investor Tracker";
            var plainTextBody = $@"Hello,

Your password has been reset for the Series A Investor Tracker.

Your new temporary password is: {newPassword}

Please log in and change your password after logging in.

If you did not request this password reset, please contact your administrator immediately.

Best regards,
Series A Investor Tracker Team";

            var htmlBody = $@"<html>
<body>
    <h2>Password Reset</h2>
    <p>Hello,</p>
    <p>Your password has been reset for the Series A Investor Tracker.</p>
    <p><strong>Your new temporary password is: {newPassword}</strong></p>
    <p>Please log in and change your password after logging in.</p>
    <p>If you did not request this password reset, please contact your administrator immediately.</p>
    <p>Best regards,<br/>Series A Investor Tracker Team</p>
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
                    Console.WriteLine($"Password reset email sent successfully to {toEmail}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Email send operation completed with status: {status}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"Email send operation returned no value");
                return false;
            }
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Azure Communication Services error sending email to {toEmail}:");
            Console.WriteLine($"  HTTP Status: {ex.Status}");
            Console.WriteLine($"  Error Code: {ex.ErrorCode ?? "N/A"}");
            Console.WriteLine($"  Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner Exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"═══════════════════════════════════════════════════════════");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email to {toEmail}: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }
}
