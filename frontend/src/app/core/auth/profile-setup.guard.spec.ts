import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Observable, firstValueFrom, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ProfileSetupService } from '../../features/teacher/profile-setup/profile-setup.service';
import { profileSetupGuard } from './profile-setup.guard';

/** מורה בלי פרופיל מלא נשלחת למסך ההקמה — אבל לא ננעלת בו. */
describe('profileSetupGuard', () => {
  let setup: { wasSkipped: () => boolean; isComplete: () => boolean | null; load: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    setup = { wasSkipped: () => false, isComplete: () => null, load: vi.fn() };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: ProfileSetupService, useValue: setup }]
    });
  });

  const run = (url: string) =>
    firstValueFrom(
      TestBed.runInInjectionContext(
        () => profileSetupGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot) as Observable<boolean | UrlTree>
      )
    );

  const serialize = (result: boolean | UrlTree) =>
    typeof result === 'boolean' ? result : TestBed.inject(Router).serializeUrl(result);

  it('פרופיל לא מלא — כל מסך באפליקציה מפנה להקמה', async () => {
    setup.load.mockReturnValue(of({ isProfileComplete: false }));
    expect(serialize(await run('/app/students'))).toBe('/app/setup');
  });

  it('פרופיל מלא — נכנסים', async () => {
    setup.load.mockReturnValue(of({ isProfileComplete: true }));
    expect(await run('/app/students')).toBe(true);
  });

  it('מסך ההקמה עצמו תמיד פתוח, אחרת ההפניה הייתה לולאה', async () => {
    setup.isComplete = () => false;
    expect(await run('/app/setup')).toBe(true);
  });

  it('"אמלא אחר כך" — ממשיכים כרגיל', async () => {
    setup.isComplete = () => false;
    setup.wasSkipped = () => true;
    expect(await run('/app/students')).toBe(true);
  });

  it('מצב ידוע לא נשלף שוב בכל ניווט', async () => {
    setup.isComplete = () => true;
    expect(await run('/app/students')).toBe(true);
    expect(setup.load).not.toHaveBeenCalled();
  });

  it('תקלת רשת בקריאת הפרופיל לא חוסמת את הכניסה', async () => {
    setup.load.mockReturnValue(throwError(() => new Error('offline')));
    expect(await run('/app/students')).toBe(true);
  });
});
