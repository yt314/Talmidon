import { HttpErrorResponse, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// מצב משותף למניעת רענונים מקבילים מרובים
let isRefreshing = false;
const refreshedToken$ = new BehaviorSubject<string | null>(null);

const AUTH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/confirm', '/auth/resend'];

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
      router.navigate(['/login']);
      return throwError(() => error);
    })
  );
}
