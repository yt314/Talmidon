import { DOCUMENT, Directive, ElementRef, HostListener, Injectable, inject } from '@angular/core';

/**
 * זוכר לאן המיקוד הלך לאחרונה, כדי שנדע להחזיר אותו כשדיאלוג נסגר.
 *
 * מאזין אחד לכל האפליקציה, נרשם בפעם הראשונה שדיאלוג כלשהו מבקש אותו.
 * שומר היסטוריה קצרה ולא רק את האחרון, כדי שדיאלוג שנפתח מתוך דיאלוג יוכל
 * למצוא את מה שהיה ממוקד לפניו ולא את מה שהיה לפני שניהם.
 */
@Injectable({ providedIn: 'root' })
export class DialogFocusHistory {
  private readonly doc = inject(DOCUMENT);
  private readonly history: HTMLElement[] = [];
  private listening = false;

  start(): void {
    if (this.listening) return;
    this.listening = true;
    // בשלב הלכידה, כי ‎focusin‎ מבועבע אבל אנחנו רוצים לראות גם מה שנעצר בדרך
    this.doc.addEventListener(
      'focusin',
      (event) => {
        const el = event.target as HTMLElement | null;
        if (!el || typeof el.focus !== 'function') return;
        this.history.push(el);
        if (this.history.length > 5) this.history.shift();
      },
      true,
    );
  }

  /** האחרון שהיה ממוקד מחוץ ל-‎container‎, אם הוא עדיין קיים במסמך. */
  lastOutside(container: HTMLElement): HTMLElement | null {
    for (let i = this.history.length - 1; i >= 0; i--) {
      const el = this.history[i];
      if (el.isConnected && !container.contains(el)) return el;
    }
    return null;
  }
}

/**
 * מחזיר את המיקוד לכפתור שפתח את הדיאלוג, כשהדיאלוג נסגר.
 *
 * ‎p-dialog‎ מעביר את המיקוד לתוך הדיאלוג בפתיחה (‎focusOnShow‎) אבל לא מחזיר
 * אותו בסגירה — אין לו בכלל הגדרה כזאת. התוצאה היא שמי שעובדת במקלדת מאבדת
 * את המקום: אחרי Escape המיקוד חוזר ל-‎body‎, וצריך לטאב מראש הדף בכל פעם
 * מחדש. מי שמשתמשת בעכבר לא מרגישה בזה בכלל.
 *
 * הבורר הוא ‎p-dialog‎ עצמו, כך שכל דיאלוג בקומפוננטה שמייבאת את ההנחיה מקבל
 * את ההתנהגות בלי לשנות את התבנית.
 */
@Directive({ selector: 'p-dialog' })
export class RestoreFocusOnCloseDirective {
  private readonly doc = inject(DOCUMENT);
  private readonly host = inject(ElementRef).nativeElement as HTMLElement;
  private readonly focusHistory = inject(DialogFocusHistory);
  private trigger: HTMLElement | null = null;

  constructor() {
    this.focusHistory.start();
  }

  @HostListener('onShow')
  protected rememberTrigger(): void {
    this.trigger = this.focusHistory.lastOutside(this.host);
  }

  @HostListener('onHide')
  protected returnFocus(): void {
    const target = this.trigger;
    this.trigger = null;
    if (!target) return;

    // אחרי סגירה יש עוד סיבוב של אנימציה והסרה מה-DOM; מחכים לו לפני שמחזירים
    setTimeout(() => {
      // הכפתור שפתח כבר לא קיים — מחיקה, למשל. עדיף להשאיר כמו שהוא מאשר
      // לקפוץ למקום שרירותי.
      if (!target.isConnected) return;
      // מישהו כבר העביר את המיקוד למקום אמיתי — לא גוזלים אותו ממנו
      const active = this.doc.activeElement;
      if (active && active !== this.doc.body && this.doc.contains(active)) return;
      target.focus();
    });
  }
}
