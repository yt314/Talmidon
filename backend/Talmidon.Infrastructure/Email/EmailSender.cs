using System.Net.Http.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Talmidon.Infrastructure.Email;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>שולח מיילים מבוסס SMTP (MailKit). תומך ב-Mailpit (פיתוח) ובספק אמיתי (פרודקשן).</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        var secureOptions = _settings.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, secureOptions, ct);

        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password ?? string.Empty, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Email sent (subject: {Subject})", subject);
    }
}

/// <summary>שולח מיילים דרך SendGrid Web API. פעיל בפרודקשן כש-SendGrid:ApiKey מוגדר (ראו DependencyInjection).</summary>
public class SendGridEmailSender : IEmailSender
{
    private readonly EmailSettings _emailSettings;
    private readonly SendGridSettings _sendGridSettings;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(IOptions<EmailSettings> emailSettings, IOptions<SendGridSettings> sendGridSettings, ILogger<SendGridEmailSender> logger)
    {
        _emailSettings = emailSettings.Value;
        _sendGridSettings = sendGridSettings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var client = new SendGridClient(_sendGridSettings.ApiKey);
        var from = new EmailAddress(_emailSettings.FromAddress, _emailSettings.FromName);
        var to = new EmailAddress(toEmail);
        var message = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: string.Empty, htmlContent: htmlBody);

        var response = await client.SendEmailAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"SendGrid send failed with status {(int)response.StatusCode}: {body}");
        }

        _logger.LogInformation("Email sent via SendGrid (subject: {Subject})", subject);
    }
}

/// <summary>שולח מיילים דרך Brevo Web API (HTTPS). עדיף על SmtpEmailSender בפלטפורמות שחוסמות SMTP יוצא (למשל Render).</summary>
public class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly EmailSettings _emailSettings;
    private readonly BrevoSettings _brevoSettings;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(HttpClient httpClient, IOptions<EmailSettings> emailSettings, IOptions<BrevoSettings> brevoSettings, ILogger<BrevoEmailSender> logger)
    {
        _httpClient = httpClient;
        _emailSettings = emailSettings.Value;
        _brevoSettings = brevoSettings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var payload = new
        {
            sender = new { name = _emailSettings.FromName, email = _emailSettings.FromAddress },
            to = new[] { new { email = toEmail } },
            subject,
            htmlContent = htmlBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", _brevoSettings.ApiKey);
        request.Headers.Add("accept", "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Brevo send failed with status {(int)response.StatusCode}: {body}");
        }

        _logger.LogInformation("Email sent via Brevo (subject: {Subject})", subject);
    }
}
