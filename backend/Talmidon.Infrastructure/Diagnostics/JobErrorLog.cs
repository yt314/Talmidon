namespace Talmidon.Infrastructure.Diagnostics;

/// <summary>
/// עוטף משימת רקע כך שכישלון שלה יגיע ליומן התקלות.
///
/// משימות הרקע הן החלק שאיש אינו מסתכל עליו: התזכורות רצות מדי שעה, יצירת
/// השיעורים מהסדרות מדי יום, ותזכורת התשלום מדי חודש — בלי שאף אחד יושב מולן.
/// כישלון שם אינו מייצר מסך אדום לאיש; הוא פשוט אומר שהתזכורת לא יצאה, ואת זה
/// מגלים כשתלמידה לא מגיעה. לכן דווקא כאן רישום הוא ההבדל היחיד בין לדעת ללא לדעת.
/// </summary>
public static class JobErrorLog
{
    public static async Task<T> RecordFailuresAsync<T>(this IErrorLog errorLog, string jobName, Func<Task<T>> run)
    {
        try
        {
            return await run();
        }
        catch (Exception exception)
        {
            await errorLog.RecordAsync(exception, new ErrorOrigin { Source = "job", Path = jobName });
            // Hangfire צריך לראות את הכישלון כדי לנסות שוב — הרישום אינו במקום הכישלון.
            throw;
        }
    }
}
