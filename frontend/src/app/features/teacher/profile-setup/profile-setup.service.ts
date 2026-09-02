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

  /** null = עדיין לא ידוע. הבאנר מוצג רק כשזה false ודאי. */
  readonly isComplete = this.complete.asReadonly();
  readonly needsSetup = computed(() => this.complete() === false);
  readonly wasSkipped = this.skipped.asReadonly();

  load(): Observable<TeacherProfile> {
    return this.profileService.getMyProfile().pipe(tap(p => this.complete.set(p.isProfileComplete)));
  }

  refresh(): void {
    this.profileService.getMyProfile().subscribe({
      next: p => this.complete.set(p.isProfileComplete),
      error: () => undefined
    });
  }

  skip(): void {
    this.skipped.set(true);
  }

  /** נקרא בהתנתקות, כדי שהמשתמשת הבאה לא תירש את המצב הקודם. */
  reset(): void {
    this.complete.set(null);
    this.skipped.set(false);
  }
}
