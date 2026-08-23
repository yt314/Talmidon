using System.Net;

namespace Talmidon.Infrastructure.Email;

/// <summary>תבנית HTML משותפת למיילי מערכת פשוטים (כותרת + פתיח + רשימת שורות), עברית RTL.</summary>
public static class EmailTemplates
{
    public static string SimpleListEmail(string title, string greeting, string introLine, IEnumerable<string> lines) =>
        $"""
        <div dir="rtl" style="font-family:Arial,sans-serif">
          <h2>{WebUtility.HtmlEncode(title)}</h2>
          <p>{WebUtility.HtmlEncode(greeting)}</p>
          <p>{WebUtility.HtmlEncode(introLine)}</p>
          <ul>{string.Join("", lines)}</ul>
        </div>
        """;
}
