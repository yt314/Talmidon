import { Translation } from 'primeng/api';

/**
 * שמות הימים בעברית, לפי אינדקס של <c>Date.getDay()</c> — ראשון הוא 0.
 * מיוצא בנפרד כי גם מסכים שלנו מציגים ימים ולא רק רכיבי PrimeNG.
 */
export const HEBREW_DAY_NAMES = ['ראשון', 'שני', 'שלישי', 'רביעי', 'חמישי', 'שישי', 'שבת'];

/**
 * תרגום מובנה של PrimeNG. בלעדיו כל רכיב שמייצר טקסט בעצמו — בוחרי תאריך,
 * רשימות בחירה, טבלאות ודיאלוגי אישור — מציג אנגלית באמצע ממשק עברי.
 *
 * <c>firstDayOfWeek: 0</c> כי בישראל השבוע מתחיל בראשון, ו-<c>dateFormat</c>
 * בפורמט יום/חודש/שנה. שימו לב ש-<c>yy</c> הוא שנה מלאה בקונבנציה של PrimeNG.
 */
export const HEBREW_TRANSLATION: Translation = {
  // ----- לוח שנה -----
  dayNames: HEBREW_DAY_NAMES,
  dayNamesShort: ['א׳', 'ב׳', 'ג׳', 'ד׳', 'ה׳', 'ו׳', 'ש׳'],
  dayNamesMin: ['א', 'ב', 'ג', 'ד', 'ה', 'ו', 'ש'],
  monthNames: [
    'ינואר', 'פברואר', 'מרץ', 'אפריל', 'מאי', 'יוני',
    'יולי', 'אוגוסט', 'ספטמבר', 'אוקטובר', 'נובמבר', 'דצמבר'
  ],
  monthNamesShort: [
    'ינו׳', 'פבר׳', 'מרץ', 'אפר׳', 'מאי', 'יוני',
    'יולי', 'אוג׳', 'ספט׳', 'אוק׳', 'נוב׳', 'דצמ׳'
  ],
  dateFormat: 'dd/mm/yy',
  firstDayOfWeek: 0,
  today: 'היום',
  weekHeader: 'שבוע',
  chooseYear: 'בחירת שנה',
  chooseMonth: 'בחירת חודש',
  chooseDate: 'בחירת תאריך',
  prevDecade: 'העשור הקודם',
  nextDecade: 'העשור הבא',
  prevYear: 'השנה הקודמת',
  nextYear: 'השנה הבאה',
  prevMonth: 'החודש הקודם',
  nextMonth: 'החודש הבא',
  prevHour: 'שעה קודמת',
  nextHour: 'שעה הבאה',
  prevMinute: 'דקה קודמת',
  nextMinute: 'דקה הבאה',
  prevSecond: 'שנייה קודמת',
  nextSecond: 'שנייה הבאה',
  am: 'לפנה״צ',
  pm: 'אחה״צ',

  // ----- בחירה וחיפוש -----
  emptyMessage: 'אין תוצאות',
  emptyFilterMessage: 'אין תוצאות',
  emptySearchMessage: 'אין תוצאות',
  emptySelectionMessage: 'לא נבחר פריט',
  searchMessage: '{0} תוצאות זמינות',
  selectionMessage: '{0} פריטים נבחרו',

  // ----- סינון בטבלאות -----
  startsWith: 'מתחיל ב',
  contains: 'מכיל',
  notContains: 'לא מכיל',
  endsWith: 'מסתיים ב',
  equals: 'שווה ל',
  notEquals: 'לא שווה ל',
  noFilter: 'ללא סינון',
  lt: 'קטן מ',
  lte: 'קטן או שווה ל',
  gt: 'גדול מ',
  gte: 'גדול או שווה ל',
  is: 'הוא',
  isNot: 'אינו',
  before: 'לפני',
  after: 'אחרי',
  dateIs: 'התאריך הוא',
  dateIsNot: 'התאריך אינו',
  dateBefore: 'לפני התאריך',
  dateAfter: 'אחרי התאריך',
  matchAll: 'התאמה לכל התנאים',
  matchAny: 'התאמה לאחד התנאים',
  addRule: 'הוספת תנאי',
  removeRule: 'הסרת תנאי',
  apply: 'החלה',
  clear: 'ניקוי',

  // ----- פעולות -----
  accept: 'אישור',
  reject: 'ביטול',
  choose: 'בחירה',
  upload: 'העלאה',
  cancel: 'ביטול',
  pending: 'ממתין',
  completed: 'הושלם',
  fileSizeTypes: ['בתים', 'ק״ב', 'מ״ב', 'ג״ב', 'ט״ב', 'פ״ב', 'א״ב', 'ז״ב', 'י״ב'],
  fileChosenMessage: '{0} קבצים נבחרו',
  noFileChosenMessage: 'לא נבחר קובץ',

  // ----- חוזק סיסמה -----
  passwordPrompt: 'הזינו סיסמה',
  weak: 'חלשה',
  medium: 'בינונית',
  strong: 'חזקה',

  aria: {
    trueLabel: 'כן',
    falseLabel: 'לא',
    nullLabel: 'ללא בחירה',
    star: 'כוכב אחד',
    stars: '{star} כוכבים',
    selectAll: 'בחירת הכל',
    unselectAll: 'ביטול בחירת הכל',
    close: 'סגירה',
    previous: 'הקודם',
    next: 'הבא',
    navigation: 'ניווט',
    scrollTop: 'גלילה למעלה',
    moveTop: 'העברה לראש',
    moveUp: 'העברה למעלה',
    moveDown: 'העברה למטה',
    moveBottom: 'העברה לתחתית',
    moveToTarget: 'העברה לרשימת היעד',
    moveToSource: 'העברה לרשימת המקור',
    moveAllToTarget: 'העברת הכל לרשימת היעד',
    moveAllToSource: 'העברת הכל לרשימת המקור',
    pageLabel: 'עמוד {page}',
    firstPageLabel: 'העמוד הראשון',
    lastPageLabel: 'העמוד האחרון',
    nextPageLabel: 'העמוד הבא',
    prevPageLabel: 'העמוד הקודם',
    previousPageLabel: 'העמוד הקודם',
    rowsPerPageLabel: 'שורות בעמוד',
    jumpToPageDropdownLabel: 'מעבר לעמוד',
    jumpToPageInputLabel: 'מעבר לעמוד',
    selectRow: 'בחירת שורה',
    unselectRow: 'ביטול בחירת שורה',
    expandRow: 'הרחבת שורה',
    collapseRow: 'כיווץ שורה',
    showFilterMenu: 'הצגת תפריט סינון',
    hideFilterMenu: 'הסתרת תפריט סינון',
    filterOperator: 'אופרטור סינון',
    filterConstraint: 'תנאי סינון',
    editRow: 'עריכת שורה',
    saveEdit: 'שמירת העריכה',
    cancelEdit: 'ביטול העריכה',
    listView: 'תצוגת רשימה',
    gridView: 'תצוגת רשת',
    slide: 'שקופית',
    slideNumber: 'שקופית {slideNumber}',
    zoomImage: 'הגדלת התמונה',
    zoomIn: 'הגדלה',
    zoomOut: 'הקטנה',
    rotateRight: 'סיבוב ימינה',
    rotateLeft: 'סיבוב שמאלה',
    listLabel: 'רשימת אפשרויות',
    selectColor: 'בחירת צבע',
    removeLabel: 'הסרה',
    browseFiles: 'עיון בקבצים',
    maximizeLabel: 'הגדלה למסך מלא',
    minimizeLabel: 'הקטנה'
  }
};
