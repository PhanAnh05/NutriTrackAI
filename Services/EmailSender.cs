using System.Net;
using System.Net.Mail;

namespace NutriTrackAI.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var smtpSection = _configuration.GetSection("SmtpSettings");
        var host = smtpSection["Host"];
        var portStr = smtpSection["Port"];
        var enableSsl = bool.TryParse(smtpSection["EnableSsl"], out var ssl) && ssl;
        var userName = smtpSection["UserName"];
        var password = smtpSection["Password"];
        var senderEmail = smtpSection["SenderEmail"] ?? "no-reply@nutritrackai.com";
        var senderName = smtpSection["SenderName"] ?? "NutriTrackAI";

        // Ghi log link/thông tin ra Console trong môi trường Dev
        _logger.LogInformation("================== [NutriTrackAI EMAIL SENDER] ==================");
        _logger.LogInformation("To: {Email}", email);
        _logger.LogInformation("Subject: {Subject}", subject);
        _logger.LogInformation("Content:\n{Message}", htmlMessage);
        _logger.LogInformation("=================================================================");

        // Nếu thông tin cấu hình là mẫu (chưa thay đổi) thì chỉ log Console
        if (string.IsNullOrWhiteSpace(userName) || userName.Contains("your-email@gmail.com") ||
            string.IsNullOrWhiteSpace(password) || password.Contains("your-app-password"))
        {
            _logger.LogWarning("Chưa cấu hình tài khoản SMTP thật trong appsettings.json. Email được hiển thị trong log trên.");
            return;
        }

        try
        {
            int port = int.TryParse(portStr, out var p) ? p : 587;
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(userName, password),
                EnableSsl = enableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Đã gửi email thành công đến {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email qua SMTP đến {Email}", email);
        }
    }
}
