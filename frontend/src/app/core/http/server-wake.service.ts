import { Injectable, signal } from '@angular/core';

/**
 * עוקב אחרי בקשות שנמשכות חריג, כדי שאפשר יהיה לומר למבקר מה קורה.
 *
 * הרקע: בתוכנית החינמית של Render השרת נרדם אחרי כמה דקות בלי תנועה, והבקשה
 * הראשונה אחרי זה ממתינה עד דקה. ספינר שמסתובב דקה שלמה בלי מילה אחת נקרא
 * כ"האתר שבור" — ודווקא מי שרואה אותו ראשונה היא תלמידה מתעניינת שאין לה שום
 * סיבה להמתין. משפט אחד משנה את זה לגמרי.
 */
@Injectable({ providedIn: 'root' })
export class ServerWakeService {
  /** מזהי הבקשות שכבר עברו את הסף. נספרות ולא נדלקות/נכבות, כי כמה בקשות רצות במקביל. */
  private readonly slow = new Set<number>();

  readonly waking = signal(false);

  markSlow(id: number): void {
    this.slow.add(id);
    this.waking.set(true);
  }

  finished(id: number): void {
    this.slow.delete(id);
    if (this.slow.size === 0) this.waking.set(false);
  }
}
