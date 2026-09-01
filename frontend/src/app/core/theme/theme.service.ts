import { DOCUMENT, Injectable, inject, signal } from '@angular/core';

/** בהיר / כהה / לפי הגדרת מערכת ההפעלה. */
export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'talmidon.theme';

/**
 * ניהול מצב בהיר/כהה. מוסיף או מסיר את המחלקה ‎.app-dark‎ מ-‎<html>‎ — אותו סלקטור
 * שהוגדר ל-PrimeNG ב-‎providePrimeNG({ theme: { options: { darkModeSelector } } })‎,
 * כך שכל טוקני הערכה (וגם הטוקנים שלנו, ‎--p-talmidon-*‎) מתחלפים יחד.
 *
 * ברירת המחדל היא 'system': המצב עוקב אחרי הגדרת מערכת ההפעלה עד שהמשתמש בוחר
 * ידנית. הבחירה נשמרת ב-localStorage ומוחלת מחדש בטעינה הבאה.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly media = this.document.defaultView?.matchMedia('(prefers-color-scheme: dark)');

  private readonly _mode = signal<ThemeMode>(this.readStoredMode());
  /** המצב שנבחר (כולל 'system'). */
  readonly mode = this._mode.asReadonly();

  private readonly _isDark = signal(false);
  /** האם בפועל מוצג כרגע מצב כהה — מה ש'system' התרגם אליו. */
  readonly isDark = this._isDark.asReadonly();

  constructor() {
    this.apply(this._mode());
    // כשהמשתמש במצב 'system' והמערכת מתחלפת (למשל מצב לילה אוטומטי) — עוקבים אחריה
    this.media?.addEventListener('change', () => {
      if (this._mode() === 'system') this.apply('system');
    });
  }

  /** מעבר מהיר בין בהיר לכהה — הכפתור בסרגל העליון. */
  toggle(): void {
    this.set(this._isDark() ? 'light' : 'dark');
  }

  set(mode: ThemeMode): void {
    this._mode.set(mode);
    try {
      if (mode === 'system') localStorage.removeItem(STORAGE_KEY);
      else localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // גלישה פרטית / אחסון חסום — הבחירה פשוט לא תישמר לפעם הבאה
    }
    this.apply(mode);
  }

  private apply(mode: ThemeMode): void {
    const dark = mode === 'dark' || (mode === 'system' && (this.media?.matches ?? false));
    this._isDark.set(dark);
    this.document.documentElement.classList.toggle('app-dark', dark);
  }

  private readStoredMode(): ThemeMode {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      return stored === 'light' || stored === 'dark' ? stored : 'system';
    } catch {
      return 'system';
    }
  }
}
