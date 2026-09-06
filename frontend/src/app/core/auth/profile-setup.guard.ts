import { inject } from '@angular/core';
import { CanActivateChildFn, Router, UrlTree } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';
import { ProfileSetupService } from '../../features/teacher/profile-setup/profile-setup.service';

/**
 * מפנה מורה שהפרופיל הציבורי שלה עדיין לא מולא אל מסך ההקמה, כדי שהצעד הראשון
 * אחרי ההתחברות יהיה להפוך את הכרטיס שלה לשמיש.
 *
 * שומר על הילדים של ‎/app‎ ולא על מסך יחיד: כשהשמירה ישבה על לוח הבקרה בלבד,
 * כל פריט אחר בסרגל היה יציאה מההקמה בלחיצה אחת.
 *
 * לא מלכודת: מי שבחרה "אמלא אחר כך" ממשיכה כרגיל לשארית הסשן, ובסרגל נשאר באנר
 * שמזכיר. אם קריאת הפרופיל נכשלת — נכנסים בלי הפניה, כי תקלת רשת לא אמורה
 * לחסום את הכניסה למערכת.
 */
export const profileSetupGuard: CanActivateChildFn = (_childRoute, state) => {
  // מסך ההקמה עצמו חייב להישאר פתוח, אחרת ההפניה אליו הייתה לולאה
  if (state.url.startsWith('/app/setup')) return of(true);

  const setup = inject(ProfileSetupService);
  const router = inject(Router);

  if (setup.wasSkipped()) return of(true);

  const known = setup.isComplete();
  if (known !== null) return of(known ? true : router.createUrlTree(['/app/setup']));

  return setup.load().pipe(
    map(profile => (profile.isProfileComplete ? true : router.createUrlTree(['/app/setup']))),
    catchError(() => of(true))
  ) as Observable<boolean | UrlTree>;
};
