import { HttpErrorResponse, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// מצב משותף למניעת רענונים מקבילים מרובים
let isRefreshing = false;
const refreshedToken$ = new BehaviorSubject<string | null>(null);

const AUTH_PATHS = [
  '/auth/login',
  '/auth/register',
  '/auth/refresh',
  '/auth/confirm',
  '/auth/resend',
  '/auth/set-password',
  '/auth/forgot-password'
];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const isAuthCall = AUTH_PATHS.some(p => req.url.includes(p));
  const token = auth.accessToken();

  const authReq = token && !isAuthCall ? withBearer(req, token) : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isAuthCall && auth.refreshTokenValue()) {
        return handle401(authReq, next, auth, router);
      }
      return throwError(() => error);
    })
  );
};

/**
 * What to tell the login screen when the session expired mid-work.
 *
 * `expired` so the screen can explain what happened. Without it the user was
 * dropped onto a blank login form in the middle of marking a lesson, with no
 * hint as to why.
 *
 * `returnUrl` so she lands back where she was. The route guard already does
 * this when someone opens a protected URL without a session; this path — the
 * only one that fires while she is working — did not.
 */
function expiredSessionParams(currentUrl: string): Record<string, string | number> {
  const params: Record<string, string | number> = { expired: 1 };
  // Returning to /login itself is not a destination
  if (currentUrl.startsWith('/') && !currentUrl.startsWith('//') && !currentUrl.startsWith('/login')) {
    params['returnUrl'] = currentUrl;
  }
  return params;
}

function withBearer(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

function handle401(req: HttpRequest<unknown>, next: HttpHandlerFn, auth: AuthService, router: Router) {
  if (isRefreshing) {
    // ממתינים שהרענון שכבר רץ יסתיים, ואז משחזרים את הבקשה
    return refreshedToken$.pipe(
      filter((t): t is string => t !== null),
      take(1),
      switchMap(t => next(withBearer(req, t)))
    );
  }

  isRefreshing = true;
  refreshedToken$.next(null);

  return auth.refresh().pipe(
    switchMap(newToken => {
      isRefreshing = false;
      refreshedToken$.next(newToken);
      return next(withBearer(req, newToken));
    }),
    catchError(error => {
      isRefreshing = false;
      auth.clearSession();
      router.navigate(['/login'], { queryParams: expiredSessionParams(router.url) });
      return throwError(() => error);
    })
  );
}
