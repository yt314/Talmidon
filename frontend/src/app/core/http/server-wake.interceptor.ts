import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { ServerWakeService } from './server-wake.service';

/**
 * אחרי ארבע שניות בלי תשובה, מדליק את ההודעה "השרת מתעורר".
 *
 * ארבע שניות ולא פחות: בקשה רגילה חוזרת הרבה לפני, והודעה שמהבהבת בכל פעולה
 * רגילה היא רעש שמלמדים את העין לדלג עליו. מה שאיטי מזה הוא כמעט תמיד שרת
 * שקם משינה.
 */
const SLOW_AFTER_MS = 4000;

let nextId = 0;

export const serverWakeInterceptor: HttpInterceptorFn = (req, next) => {
  const wake = inject(ServerWakeService);
  const id = nextId++;

  const timer = setTimeout(() => wake.markSlow(id), SLOW_AFTER_MS);

  return next(req).pipe(
    finalize(() => {
      clearTimeout(timer);
      wake.finished(id);
    })
  );
};
