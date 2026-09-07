namespace Talmidon.Domain;

/// <summary>
/// מה נחשב "פרופיל מולא". הכלל יושב כאן ולא בבקר כדי שהשרת והממשק ידברו על אותה
/// הגדרה בדיוק — הממשק מקבל את התוצאה מוכנה ולא מחשב אותה מחדש.
/// </summary>
public static class TeacherProfileRules
{
    /// <summary>
    /// פרופיל נחשב מלא כשיש תחום הוראה אחד לפחות, מחיר לשיעור, ודרך ליצור קשר.
    /// אלה שלושת הדברים שהורה מחפש בכרטיס בספרייה; בלעדיהם הכרטיס חסר משמעות.
    /// תיאור ותמונה נחשבים רצויים אך לא חובה.
    ///
    /// מקבל ערכים ולא ישות, כדי שהקורא יוכל להטיל עמודות בלבד ולא לטעון את שורת
    /// המורה כולה (שכוללת את ה-blob של התמונה).
    /// </summary>
    public static bool IsComplete(
        int subjectCount,
        decimal defaultPricePerLesson,
        string? phone,
        string? contactEmail,
        string? legacyContactInfo) =>
        subjectCount > 0
        && defaultPricePerLesson > 0
        // די בדרך אחת ליצור קשר, ומורה שמילאה אחת מהן אינה אמורה להיתקע
        // במסך ההקמה בגלל השנייה.
        //
        // הטקסט החופשי הישן עדיין נספר, אף שאינו נערך יותר: מורה שהשלימה את
        // הפרופיל לפניו הייתה מוצאת אותו לא-מלא פתאום, ונשלחת להקמה בכל כניסה.
        && (!string.IsNullOrWhiteSpace(phone)
            || !string.IsNullOrWhiteSpace(contactEmail)
            || !string.IsNullOrWhiteSpace(legacyContactInfo));
}
