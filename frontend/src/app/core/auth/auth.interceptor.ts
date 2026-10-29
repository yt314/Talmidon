import { HttpErrorResponse, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AsyncSubject, Observable, catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * הרענון שרץ כרגע, משותף לכל הבקשות שנכשלו בזמנו. רענון אחד בלבד: שניים במקביל
 * היו שולחים את אותו אסימון רענון פעמיים, והשרת מזהה בזה שימוש חוזר ומבטל הכול.
 */
let refreshInFlight: Observable<string> | null = null;

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
 * מה לומר למסך ההתחברות כשההפעלה פקעה באמצע העבודה.
 *
 * ‎expired‎ — כדי שהמסך יסביר מה קרה. בלעדיו המורה נזרקה למסך התחברות ריק באמצע
 * סימון שיעור, בלי שום סימן למה.
 *
 * ‎returnUrl‎ — כדי שתחזור לאן שהייתה. שומר המסלול כבר עושה את זה כשנכנסים לכתובת
 * מוגנת בלי הפעלה; הנתיב הזה, שהוא היחיד שקורה תוך כדי עבודה, לא עשה.
 */
function expiredSessionParams(currentUrl: string): Record<string, string | number> {
  const params: Record<string, string | number> = { expired: 1 };
  // חזרה אל ‎/login‎ עצמו אינה יעד
  if (currentUrl.startsWith('/') && !currentUrl.startsWith('//') && !currentUrl.startsWith('/login')) {
    params['returnUrl'] = currentUrl;
  }
  return params;
}

function withBearer(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

function handle401(req: HttpRequest<unknown>, next: HttpHandlerFn, auth: AuthService, router: Router) {
  return sharedRefresh(auth, router).pipe(switchMap(token => next(withBearer(req, token))));
}

/**
 * הרענון נרשם כאן ולא דרך הבקשה שגילתה את ה-401, כי לבקשה מותר להתבטל: מונה
 * ההתראות מחליף בקשה בכל דקה ובכל מעבר לשונית, ומסך שעוזבים מבטל את שלו. כשהרענון
 * היה תלוי בה, ביטול באמצע השאיר אותו "רץ" לנצח — וכל בקשה שפקעה אחר כך חיכתה לו
 * עד רענון הדף, מה שנראה בדיוק כמו שרת מת.
 */
function sharedRefresh(auth: AuthService, router: Router): Observable<string> {
  if (refreshInFlight) return refreshInFlight;

  const result = new AsyncSubject<string>();
  refreshInFlight = result;
  auth.refresh().subscribe({
    next: token => result.next(token),
    error: error => {
      refreshInFlight = null;
      auth.clearSession();
      router.navigate(['/login'], { queryParams: expiredSessionParams(router.url) });
      result.error(error);
    },
    complete: () => {
      refreshInFlight = null;
      result.complete();
    }
  });
  return result;
}
