import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { TeacherProfile } from '../profile/profile.models';
import { TeacherProfileService } from '../profile/profile.service';

/**
 * מחזיק את מצב "האם הפרופיל מולא" עבור השומר (guard) והבאנר בסרגל.
 *
 * נשמר במטמון כאן ולא נשלף בכל ניווט — אחרת כל מעבר בין מסכים היה יוצר קריאת
 * רשת נוספת. מסך ההקמה קורא ל-refresh אחרי שמירה.
 */
@Injectable({ providedIn: 'root' })
export class ProfileSetupService {
  private readonly profileService = inject(TeacherProfileService);

  private readonly complete = signal<boolean | null>(null);
  /** המורה בחרה "אמלא אחר כך" — לתוקף הסשן הנוכחי בלבד. */
  private readonly skipped = signal(false);
  /** הבאנר נסגר בלחיצה על ה-X. חוזר בכניסה הבאה, כדי שהתזכורת לא תיעלם לתמיד. */
  private readonly bannerDismissed = signal(false);

  /** null = עדיין לא ידוע. הבאנר מוצג רק כשזה false ודאי. */
  readonly isComplete = this.complete.asReadonly();
  readonly needsSetup = computed(() => this.complete() === false);
  readonly wasSkipped = this.skipped.asReadonly();
  readonly bannerHidden = this.bannerDismissed.asReadonly();

  dismissBanner(): void {
    this.bannerDismissed.set(true);
  }

  load(): Observable<TeacherProfile> {
    return this.profileService.getMyProfile().pipe(tap(p => this.complete.set(p.isProfileComplete)));
  }

  /**
   * מסמן את הפרופיל כמלא מיד, בלי לחכות לסבב רשת נוסף.
   *
   * השומר קורא את הערך הזה באופן סינכרוני בזמן הניווט, ולכן רענון שנשלח כבקשה
   * נפרדת מגיע מאוחר מדי: הלחיצה על "סיימתי" הייתה מנווטת ללוח הבקרה בזמן
   * שהערך עדיין false, והשומר היה מחזיר את המורה למסך ההקמה. השמירה עצמה כבר
   * אכפה את אותם תנאים שהשרת בודק (תחום, מחיר ודרך ליצור קשר), אז הערך ידוע.
   */
  markComplete(): void {
    this.complete.set(true);
  }

  /** מסנכרן את המטמון אחרי שמסך אחר קיבל מהשרת את מצב השלמות. */
  setComplete(value: boolean): void {
    this.complete.set(value);
  }

  skip(): void {
    this.skipped.set(true);
  }

  /** נקרא בהתנתקות, כדי שהמשתמשת הבאה לא תירש את המצב הקודם. */
  reset(): void {
    this.complete.set(null);
    this.skipped.set(false);
    this.bannerDismissed.set(false);
  }
}
