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

/// <summary>
/// הגדרות SendGrid (נטענות מ-section "SendGrid"). כשה-ApiKey מוגדר (בפרודקשן, דרך משתנה הסביבה
/// SendGrid__ApiKey) המערכת שולחת מיילים דרך SendGrid Web API במקום SMTP/Mailpit.
/// </summary>
public class SendGridSettings
{
    public string? ApiKey { get; set; }
}

/// <summary>
/// הגדרות Brevo (נטענות מ-section "Brevo"). כשה-ApiKey מוגדר (דרך משתנה הסביבה Brevo__ApiKey)
/// המערכת שולחת מיילים דרך Brevo Web API (HTTPS) — נחוץ בפלטפורמות כמו Render שחוסמות SMTP יוצא.
/// </summary>
public class BrevoSettings
{
    public string? ApiKey { get; set; }
}
