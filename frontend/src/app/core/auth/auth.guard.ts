import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from './auth.models';

/** דורש משתמש מחובר. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};

/** דורש משתמש מחובר בעל אחד מהתפקידים הנתונים. */
export const roleGuard = (allowed: Role[]): CanActivateFn => () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated() && allowed.some(role => auth.hasRole(role))) {
    return true;
  }
  return router.createUrlTree([auth.isAuthenticated() ? auth.homePath() : '/login']);
};
