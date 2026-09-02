import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { ProfileSetupService } from '../../features/teacher/profile-setup/profile-setup.service';

/**
 * מפנה מורה שהפרופיל הציבורי שלה עדיין לא מולא אל מסך ההקמה, כדי שהצעד הראשון
 * אחרי ההתחברות יהיה להפוך את הכרטיס שלה לשמיש.
 *
 * לא מלכודת: מי שבחרה "אמלא אחר כך" ממשיכה כרגיל לשארית הסשן, ובסרגל נשאר באנר
 * שמזכיר. אם קריאת הפרופיל נכשלת — נכנסים בלי הפניה, כי תקלת רשת לא אמורה
 * לחסום את הכניסה למערכת.
 */
export const profileSetupGuard: CanActivateFn = () => {
  const setup = inject(ProfileSetupService);
  const router = inject(Router);

  if (setup.wasSkipped()) return of(true);

  const known = setup.isComplete();
  if (known !== null) return of(known ? true : router.createUrlTree(['/app/setup']));

  return setup.load().pipe(
    map(profile => (profile.isProfileComplete ? true : router.createUrlTree(['/app/setup']))),
    catchError(() => of(true))
  );
};
