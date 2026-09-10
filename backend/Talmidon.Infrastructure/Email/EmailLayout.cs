using System.Net;

namespace Talmidon.Infrastructure.Email;

/// <summary>תוכן מייל, בנוי מחלקים. מה שאין — פשוט לא מוצג.</summary>
public sealed record EmailMessage(
    string Title,
    string? Greeting = null,
    string? Intro = null,
    IReadOnlyList<(string Label, string Value)>? Details = null,
    IReadOnlyList<string>? Items = null,
    string? Quote = null,
    string? ActionLabel = null,
    string? ActionUrl = null,
    string? Note = null);

/// <summary>
/// עיצוב אחיד לכל מייל שיוצא מתלמידון.
///
/// כתוב בטבלאות ובסגנון בשורה, ולא ב-flex וב-CSS חיצוני: לקוחות דואר — Outlook בראשם —
/// מתעלמים מגיליונות סגנון ומפריסות מודרניות, ומייל "נקי" נשבר אצלם לערימת טקסט.
/// הכפתור בנוי מטבלה מאותה סיבה, ולא מ-‎&lt;a&gt;‎ עם padding.
///
/// כל ערך עובר קידוד HTML. התוכן מגיע משמות, מהודעות ומטפסים ציבוריים — טקסט של
/// משתמש שנכנס כ-HTML הוא איך שמייל הופך לכלי של מישהו אחר.
/// </summary>
public static class EmailLayout
{
    private const string Brand = "#0d9488";      // טורקיז המותג
    private const string BrandDark = "#115e59";
    private const string Ink = "#1f2937";
    private const string Muted = "#6b7280";
    private const string Line = "#e5e7eb";
    private const string Page = "#f4f5f7";

    public static string Render(EmailMessage message)
    {
        var body =
            Paragraph(message.Greeting, Ink, "16px") +
            Paragraph(message.Intro, Ink, "16px") +
            DetailsBlock(message.Details) +
            ItemsBlock(message.Items) +
            QuoteBlock(message.Quote) +
            Button(message.ActionLabel, message.ActionUrl) +
            Paragraph(message.Note, Muted, "13px");

        return $"""
            <div dir="rtl" style="margin:0;padding:0;background:{Page}">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
                     style="background:{Page};padding:24px 12px">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0"
                           style="width:100%;max-width:600px;background:#ffffff;border:1px solid {Line};border-radius:12px;overflow:hidden;font-family:Arial,Helvetica,sans-serif">
                      <tr>
                        <td style="background:{BrandDark};padding:16px 24px;color:#ffffff;font-size:18px;font-weight:bold;text-align:right">
                          תלמידון
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px;text-align:right;direction:rtl">
                          <h1 style="margin:0 0 16px;font-size:20px;line-height:1.4;color:{Ink}">{Encode(message.Title)}</h1>
                          {body}
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:16px 24px;border-top:1px solid {Line};color:{Muted};font-size:12px;text-align:right">
                          נשלח אוטומטית מתלמידון — מערכת הניהול למורות פרטיות.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </div>
            """;
    }

    private static string Paragraph(string? text, string color, string size) =>
        string.IsNullOrWhiteSpace(text)
            ? ""
            : $"""<p style="margin:0 0 14px;font-size:{size};line-height:1.6;color:{color}">{Encode(text)}</p>""";

    /// <summary>שורות "תווית: ערך" — שם, טלפון, תאריך. טבלה, כדי שהיישור יחזיק בכל לקוח.</summary>
    private static string DetailsBlock(IReadOnlyList<(string Label, string Value)>? details)
    {
        if (details is null || details.Count == 0) return "";

        var rows = details
            .Where(d => !string.IsNullOrWhiteSpace(d.Value))
            .Select(d => $"""
                <tr>
                  <td style="padding:6px 0;font-size:14px;color:{Muted};white-space:nowrap">{Encode(d.Label)}</td>
                  <td style="padding:6px 0 6px 12px;font-size:15px;color:{Ink}">{Encode(d.Value)}</td>
                </tr>
                """);

        return $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0"
                   style="width:100%;margin:0 0 16px;direction:rtl;text-align:right">
              {string.Join("", rows)}
            </table>
            """;
    }

    private static string ItemsBlock(IReadOnlyList<string>? items)
    {
        if (items is null || items.Count == 0) return "";

        var lines = items.Select(item => $"""
            <li style="padding:4px 0;font-size:15px;line-height:1.6;color:{Ink}">{Encode(item)}</li>
            """);

        return $"""<ul style="margin:0 0 16px;padding:0 20px 0 0;direction:rtl;text-align:right">{string.Join("", lines)}</ul>""";
    }

    /// <summary>ציטוט של מה שנכתב — הודעה, פנייה, סיבה לבקשה.</summary>
    private static string QuoteBlock(string? quote) =>
        string.IsNullOrWhiteSpace(quote)
            ? ""
            : $"""
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="width:100%;margin:0 0 16px">
                <tr>
                  <td style="border-right:3px solid {Brand};background:#f8fafa;padding:12px 14px;font-size:15px;line-height:1.6;color:{Ink};white-space:pre-wrap;text-align:right">{Encode(quote)}</td>
                </tr>
              </table>
              """;

    /// <summary>
    /// כפתור. רק כתובת http/https מרונדרת — כתובת מסוג אחר בתוך מייל היא דרך להריץ
    /// משהו אצל מי שלוחצת, וכאן אין שום סיבה לתמוך בה.
    /// </summary>
    private static string Button(string? label, string? url)
    {
        if (string.IsNullOrWhiteSpace(label) || !IsSafeUrl(url)) return "";

        return $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:4px 0 18px">
              <tr>
                <td align="center" bgcolor="{Brand}" style="border-radius:8px">
                  <a href="{Encode(url!)}"
                     style="display:inline-block;padding:12px 28px;font-size:16px;font-weight:bold;color:#ffffff;text-decoration:none;border-radius:8px">
                    {Encode(label)}
                  </a>
                </td>
              </tr>
            </table>
            """;
    }

    private static bool IsSafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
        (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
