import { inject } from '@angular/core';
import { CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from './auth.models';

/**
 * שולח להתחברות וזוכר לאן רצו להגיע.
 *
 * בלי זה, קישור שמגיע במייל — "לצפייה בפנייה" — מוביל את מי שאינה מחוברת למסך
 * ההתחברות, ואחריו לעמוד הבית. היא נחתה במערכת בלי הפנייה שבגללה לחצה.
 */
const toLogin = (router: Router, state: RouterStateSnapshot) =>
  router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

/** דורש משתמש מחובר. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : toLogin(router, state);
};

/** דורש משתמש מחובר בעל אחד מהתפקידים הנתונים. */
export const roleGuard = (allowed: Role[]): CanActivateFn => (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated() && allowed.some(role => auth.hasRole(role))) {
    return true;
  }

  // מחוברת אך בתפקיד אחר — אין טעם לזכור יעד שלא יהיה מותר לה גם אחרי התחברות
  return auth.isAuthenticated() ? router.createUrlTree([auth.homePath()]) : toLogin(router, state);
};
