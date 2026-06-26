namespace Talmidon.Infrastructure.Email;

/// <summary>הגדרות SMTP (נטענות מ-section "Email"). בפיתוח מצביעות ל-Mailpit (localhost:1025).</summary>
public class EmailSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; }
    public string FromAddress { get; set; } = "no-reply@talmidon.local";
    public string FromName { get; set; } = "תלמידון";
    public string? Username { get; set; }
    public string? Password { get; set; }
}
