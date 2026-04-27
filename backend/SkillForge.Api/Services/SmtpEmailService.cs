using System.Net;
using System.Net.Mail;

namespace SkillForge.Api.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrEmpty(host))
        {
            _logger.LogWarning("SMTP not configured — skipping email to {To}: {Subject}", to, subject);
            return;
        }

        var port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
        var from = _configuration["Smtp:From"] ?? "noreply@skillforge.app";
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var enableSsl = !string.Equals(_configuration["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

        using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            client.Credentials = new NetworkCredential(username, password);

        using var message = new MailMessage(from, to, subject, htmlBody) { IsBodyHtml = true };
        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
    }
}
