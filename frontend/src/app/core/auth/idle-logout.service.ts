import { DOCUMENT, Injectable, NgZone, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { IdleState, idleState, secondsUntilLogout } from './idle';

const LAST_ACTIVITY_KEY = 'talmidon_last_activity';

/**
 * קצב הבדיקה: נדיר כל עוד שקט, וכל שנייה כשהאזהרה על המסך — הספירה לאחור צריכה
 * להיראות כמו ספירה לאחור.
 */
const CHECK_EVERY_MS = 10_000;
const CHECK_WHILE_WARNING_MS = 1_000;
const WRITE_EVERY_MS = 10_000;

/**
 * מנתק אחרי חוסר פעילות.
 *
 * הטווח הארוך (אסימון הרענון, שבועיים) מגן על מכשיר שאבד. זה מגן על התרחיש השכיח
 * יותר: מסך שנשאר פתוח כשקמים ממנו.
 *
 * חותמת הפעילות נשמרת ב-localStorage ולא בזיכרון, כדי שכמה לשוניות פתוחות יחלקו את
 * אותו שעון — אחרת עבודה בלשונית אחת לא הייתה מונעת ניתוק בשנייה.
 *
 * המאזינים נרשמים מחוץ ל-Zone: הם רצים על כל הקלדה ולחיצה, ובתוך ה-Zone כל אחת מהן
 * הייתה מפעילה סבב בדיקת שינויים בכל האפליקציה.
 */
@Injectable({ providedIn: 'root' })
export class IdleLogoutService {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private readonly document = inject(DOCUMENT);

  /** מוצג כשנשארה דקה. הרכיב באפליקציה קורא את זה. */
  readonly warning = signal(false);
  readonly secondsLeft = signal(0);

  private timer?: ReturnType<typeof setTimeout>;
  private running = false;
  private lastWrite = 0;

  constructor() {
    // מתחיל ונעצר עם ההתחברות — אין טעם למדוד חוסר פעילות של מי שאינה מחוברת
    effect(() => (this.auth.isAuthenticated() ? this.start() : this.stop()));
  }

  /** נקרא כשהיא בוחרת להישאר מחוברת. */
  keepAlive(): void {
    this.warning.set(false);
    this.recordActivity(true);
  }

  logoutNow(): void {
    this.stop();
    this.auth.logout();
    this.router.navigate(['/login'], { queryParams: { timeout: 1 } });
  }

  private start(): void {
    if (this.running) return;
    this.running = true;

    this.recordActivity(true);
    this.zone.runOutsideAngular(() => {
      for (const event of ['pointerdown', 'keydown', 'scroll'] as const) {
        this.document.addEventListener(event, this.onActivity, { passive: true });
      }
      // חזרה ללשונית היא פעילות לכל דבר, וגם הרגע שבו כדאי לבדוק אם עבר הזמן
      this.document.addEventListener('visibilitychange', this.onVisible);
    });
    this.scheduleNext(CHECK_EVERY_MS);
  }

  private stop(): void {
    if (!this.running) return;
    this.running = false;

    if (this.timer) clearTimeout(this.timer);
    this.timer = undefined;
    this.warning.set(false);
    for (const event of ['pointerdown', 'keydown', 'scroll'] as const) {
      this.document.removeEventListener(event, this.onActivity);
    }
    this.document.removeEventListener('visibilitychange', this.onVisible);
  }

  private readonly onActivity = () => this.recordActivity(false);

  private readonly onVisible = () => {
    if (this.document.visibilityState !== 'visible') return;
    if (this.timer) clearTimeout(this.timer);
    this.zone.run(() => this.check());
  };

  /** כתיבה מווסתת: המאזינים רצים על כל לחיצה, ואין סיבה לגעת באחסון בכל אחת. */
  private recordActivity(force: boolean): void {
    const now = Date.now();
    if (!force && now - this.lastWrite < WRITE_EVERY_MS) return;

    this.lastWrite = now;
    try {
      localStorage.setItem(LAST_ACTIVITY_KEY, String(now));
    } catch {
      // דפדפן שחוסם אחסון — נשארים עם ההתנהגות הקודמת ולא מפילים את המסך
    }
  }

  private scheduleNext(delay: number): void {
    if (!this.running) return;
    this.zone.runOutsideAngular(() => {
      this.timer = setTimeout(() => this.zone.run(() => this.check()), delay);
    });
  }

  private check(): void {
    if (!this.running) return;

    if (!this.auth.isAuthenticated()) {
      this.stop();
      return;
    }

    const now = Date.now();
    const last = this.readLastActivity() ?? now;
    const state: IdleState = idleState(last, now);

    if (state === 'expired') {
      this.logoutNow();
      return;
    }

    this.secondsLeft.set(secondsUntilLogout(last, now));
    this.warning.set(state === 'warning');
    this.scheduleNext(state === 'warning' ? CHECK_WHILE_WARNING_MS : CHECK_EVERY_MS);
  }

  private readLastActivity(): number | null {
    try {
      const raw = localStorage.getItem(LAST_ACTIVITY_KEY);
      const value = raw === null ? NaN : Number(raw);
      return Number.isFinite(value) ? value : null;
    } catch {
      return null;
    }
  }
}
