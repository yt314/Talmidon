import { ReadingPassage, WordPair } from './games.models';

/**
 * תוכן המשחקים נשמר בקוד ולא נוצר במודל שפה: כך הוא מיידי, לא עולה כסף, עובד גם בלי
 * חיבור לשרת — ובעיקר, התוכן נבדק מראש ומתאים לכל קהל.
 */

export const WORD_PAIRS: readonly WordPair[] = [
  // קל — מילים יומיומיות
  { english: 'book', hebrew: 'ספר', level: 'easy' },
  { english: 'table', hebrew: 'שולחן', level: 'easy' },
  { english: 'water', hebrew: 'מים', level: 'easy' },
  { english: 'house', hebrew: 'בית', level: 'easy' },
  { english: 'bread', hebrew: 'לחם', level: 'easy' },
  { english: 'door', hebrew: 'דלת', level: 'easy' },
  { english: 'window', hebrew: 'חלון', level: 'easy' },
  { english: 'chair', hebrew: 'כיסא', level: 'easy' },
  { english: 'pen', hebrew: 'עט', level: 'easy' },
  { english: 'friend', hebrew: 'חבר', level: 'easy' },
  { english: 'family', hebrew: 'משפחה', level: 'easy' },
  { english: 'school', hebrew: 'בית ספר', level: 'easy' },
  { english: 'teacher', hebrew: 'מורה', level: 'easy' },
  { english: 'garden', hebrew: 'גינה', level: 'easy' },
  { english: 'street', hebrew: 'רחוב', level: 'easy' },
  { english: 'morning', hebrew: 'בוקר', level: 'easy' },
  { english: 'night', hebrew: 'לילה', level: 'easy' },
  { english: 'rain', hebrew: 'גשם', level: 'easy' },
  { english: 'sun', hebrew: 'שמש', level: 'easy' },
  { english: 'tree', hebrew: 'עץ', level: 'easy' },

  // בינוני
  { english: 'question', hebrew: 'שאלה', level: 'medium' },
  { english: 'answer', hebrew: 'תשובה', level: 'medium' },
  { english: 'important', hebrew: 'חשוב', level: 'medium' },
  { english: 'together', hebrew: 'ביחד', level: 'medium' },
  { english: 'because', hebrew: 'מפני ש', level: 'medium' },
  { english: 'between', hebrew: 'בין', level: 'medium' },
  { english: 'always', hebrew: 'תמיד', level: 'medium' },
  { english: 'never', hebrew: 'אף פעם', level: 'medium' },
  { english: 'remember', hebrew: 'לזכור', level: 'medium' },
  { english: 'forget', hebrew: 'לשכוח', level: 'medium' },
  { english: 'choose', hebrew: 'לבחור', level: 'medium' },
  { english: 'explain', hebrew: 'להסביר', level: 'medium' },
  { english: 'promise', hebrew: 'הבטחה', level: 'medium' },
  { english: 'careful', hebrew: 'זהיר', level: 'medium' },
  { english: 'quiet', hebrew: 'שקט', level: 'medium' },
  { english: 'difficult', hebrew: 'קשה', level: 'medium' },
  { english: 'simple', hebrew: 'פשוט', level: 'medium' },
  { english: 'enough', hebrew: 'מספיק', level: 'medium' },
  { english: 'almost', hebrew: 'כמעט', level: 'medium' },
  { english: 'perhaps', hebrew: 'אולי', level: 'medium' },

  // מאתגר
  { english: 'responsibility', hebrew: 'אחריות', level: 'hard' },
  { english: 'opportunity', hebrew: 'הזדמנות', level: 'hard' },
  { english: 'experience', hebrew: 'ניסיון', level: 'hard' },
  { english: 'knowledge', hebrew: 'ידע', level: 'hard' },
  { english: 'patience', hebrew: 'סבלנות', level: 'hard' },
  { english: 'honest', hebrew: 'ישר', level: 'hard' },
  { english: 'generous', hebrew: 'נדיב', level: 'hard' },
  { english: 'improve', hebrew: 'לשפר', level: 'hard' },
  { english: 'achieve', hebrew: 'להשיג', level: 'hard' },
  { english: 'succeed', hebrew: 'להצליח', level: 'hard' },
  { english: 'encourage', hebrew: 'לעודד', level: 'hard' },
  { english: 'describe', hebrew: 'לתאר', level: 'hard' },
  { english: 'compare', hebrew: 'להשוות', level: 'hard' },
  { english: 'decide', hebrew: 'להחליט', level: 'hard' },
  { english: 'suggest', hebrew: 'להציע', level: 'hard' },
  { english: 'accept', hebrew: 'לקבל', level: 'hard' },
  { english: 'refuse', hebrew: 'לסרב', level: 'hard' },
  { english: 'measure', hebrew: 'למדוד', level: 'hard' },
  { english: 'discover', hebrew: 'לגלות', level: 'hard' },
  { english: 'imagine', hebrew: 'לדמיין', level: 'hard' }
];

export const READING_PASSAGES: readonly ReadingPassage[] = [
  {
    title: 'הדבורה והפרח',
    level: 'easy',
    text: `דבורה קטנה יצאה בבוקר לחפש אוכל. היא עברה מפרח לפרח ואספה צוף.
בכל פרח שבו נחתה, נדבק לרגליה קצת אבקה. כשעברה לפרח הבא, האבקה נשרה עליו.
כך, בלי לדעת, עזרה הדבורה לפרחים לגדול. בערב חזרה לכוורת עם הצוף, ומהצוף נעשה דבש.`,
    questions: [
      {
        question: 'מה אספה הדבורה מהפרחים?',
        options: ['צוף', 'מים', 'עלים', 'זרעים'],
        correctIndex: 0
      },
      {
        question: 'כיצד עזרה הדבורה לפרחים?',
        options: [
          'השקתה אותם',
          'העבירה אבקה מפרח לפרח',
          'הגנה עליהם מהשמש',
          'שתלה אותם מחדש'
        ],
        correctIndex: 1
      },
      {
        question: 'מה נעשה מהצוף?',
        options: ['שעווה', 'דבש', 'שמן', 'קמח'],
        correctIndex: 1
      }
    ]
  },
  {
    title: 'הגשר של הנהר',
    level: 'easy',
    text: `בכפר קטן זרם נהר רחב, ולא היה עליו גשר. כל בוקר היו התושבים מקיפים את הנהר
בדרך ארוכה, והדרך לקחה שעה שלמה. יום אחד החליטו התושבים לבנות גשר יחד.
כל אחד הביא מה שיכול: אחד עצים, אחד חבלים, ואחד אוכל לפועלים. אחרי חודש הגשר היה מוכן,
והמעבר לקח חמש דקות בלבד.`,
    questions: [
      {
        question: 'כמה זמן לקחה הדרך לפני שנבנה הגשר?',
        options: ['חמש דקות', 'חצי שעה', 'שעה שלמה', 'יום שלם'],
        correctIndex: 2
      },
      {
        question: 'מי בנה את הגשר?',
        options: ['התושבים יחד', 'פועלים מהעיר', 'ילדי הכפר', 'איש אחד לבדו'],
        correctIndex: 0
      },
      {
        question: 'מה אפשר ללמוד מהסיפור?',
        options: [
          'שעדיף לוותר על דרכים ארוכות',
          'שעבודה משותפת מקצרת את הדרך לכולם',
          'שנהרות מסוכנים',
          'שכדאי לגור קרוב לנהר'
        ],
        correctIndex: 1
      }
    ]
  },
  {
    title: 'המפתח האבוד',
    level: 'medium',
    text: `יעל חיפשה את המפתח שלה בכל הבית. היא בדקה בתיק, על השולחן ובכיסי המעיל,
אבל המפתח לא נמצא. לפני שהתייאשה, ניסתה יעל דבר אחר: במקום לחפש בכל מקום,
נזכרה מה עשתה אתמול בערב. היא נכנסה הביתה, הניחה שקית על השיש, ופתחה את המקרר.
יעל ניגשה למקרר — והמפתח היה שם, ליד הבקבוקים.`,
    questions: [
      {
        question: 'היכן נמצא המפתח בסוף?',
        options: ['בתיק', 'במקרר', 'בכיס המעיל', 'על השולחן'],
        correctIndex: 1
      },
      {
        question: 'מה שינתה יעל בדרך החיפוש שלה?',
        options: [
          'ביקשה עזרה',
          'חיפשה מהר יותר',
          'שחזרה מה עשתה קודם במקום לחפש בכל מקום',
          'הזמינה מפתח חדש'
        ],
        correctIndex: 2
      },
      {
        question: 'מה המסר של הסיפור?',
        options: [
          'שכדאי לשמור מפתח רזרבי',
          'שמחשבה מסודרת עוזרת יותר מחיפוש אקראי',
          'שאסור לפתוח את המקרר',
          'שצריך לסדר את הבית'
        ],
        correctIndex: 1
      }
    ]
  },
  {
    title: 'שתי הכדים',
    level: 'medium',
    text: `לאיכר היו שני כדים שבהם נשא מים מהבאר. אחד הכדים היה שלם, והשני היה סדוק
ואיבד חצי מהמים בדרך. הכד הסדוק התבייש. יום אחד אמר לאיכר: "אני מצטער שאני מאבד מים".
האיכר חייך ואמר: "שמת לב שבצד שלך בשביל צומחים פרחים, ובצד השני אין? זרעתי שם זרעים,
והמים שנטפו ממך השקו אותם. בזכותך יש לנו פרחים על השולחן".`,
    questions: [
      {
        question: 'מדוע התבייש הכד הסדוק?',
        options: [
          'כי היה כבד',
          'כי איבד חצי מהמים בדרך',
          'כי היה קטן מהשני',
          'כי היה ישן'
        ],
        correctIndex: 1
      },
      {
        question: 'מה גדל בצד של הכד הסדוק?',
        options: ['עשבים', 'פרחים', 'עצים', 'ירקות'],
        correctIndex: 1
      },
      {
        question: 'מה רצה האיכר להראות לכד?',
        options: [
          'שצריך לתקן אותו',
          'שגם החיסרון שלו הביא תועלת',
          'שהכד השני טוב ממנו',
          'שהדרך לבאר ארוכה'
        ],
        correctIndex: 1
      }
    ]
  },
  {
    title: 'הזמן של הספרייה',
    level: 'hard',
    text: `ספרייה עירונית החליטה לבדוק מדוע מעט אנשים מגיעים אליה. הצוות שיער שהסיבה היא
מיעוט ספרים חדשים, ולכן הוזמנו מאות ספרים. אחרי חודשיים לא חל שינוי במספר המבקרים.
אז נערך סקר קצר בין התושבים, ומהתשובות התברר שהבעיה אחרת לגמרי: הספרייה נסגרה בארבע,
בדיוק בשעה שבה רוב האנשים סיימו את יום העבודה. הספרייה שינתה את שעות הפתיחה עד שבע בערב,
ומספר המבקרים הוכפל תוך חודש.`,
    questions: [
      {
        question: 'מה הייתה ההשערה הראשונה של הצוות?',
        options: [
          'ששעות הפתיחה אינן נוחות',
          'שחסרים ספרים חדשים',
          'שהספרייה רחוקה מדי',
          'שהתושבים אינם אוהבים לקרוא'
        ],
        correctIndex: 1
      },
      {
        question: 'מה גילה הסקר?',
        options: [
          'שהספרים אינם מעניינים',
          'שהמחיר גבוה',
          'שהספרייה נסגרה בשעה שבה אנשים עוד בעבודה',
          'שאין מקום לשבת'
        ],
        correctIndex: 2
      },
      {
        question: 'מה הלקח המרכזי מהקטע?',
        options: [
          'שכדאי לקנות ספרים חדשים',
          'שעדיף לשאול את האנשים לפני שמשקיעים בפתרון משוער',
          'שספריות צריכות להיות פתוחות תמיד',
          'שסקרים אינם אמינים'
        ],
        correctIndex: 1
      }
    ]
  }
];
