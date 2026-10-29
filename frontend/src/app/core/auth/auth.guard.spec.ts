import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { authGuard, roleGuard } from './auth.guard';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

/** מי נכנס לאן, ולאן נשלח מי שאינו מורשה. */
describe('authGuard / roleGuard', () => {
  let signedIn: boolean;
  let roles: Role[];

  beforeEach(() => {
    signedIn = false;
    roles = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: () => signedIn,
            hasRole: (role: Role) => roles.includes(role),
            homePath: () => (roles.includes('Parent') ? '/parent' : '/app/dashboard')
          }
        }
      ]
    });
  });

  const run = (guard: typeof authGuard, url: string) =>
    TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot));

  const serialize = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  /** קישור מתוך מייל ("לצפייה בפנייה") צריך להוביל לפנייה גם אחרי ההתחברות. */
  it('מי שאינה מחוברת נשלחת להתחברות, עם הכתובת שרצתה להגיע אליה', () => {
    expect(serialize(run(authGuard, '/app/contact-requests?id=7'))).toBe(
      '/login?returnUrl=%2Fapp%2Fcontact-requests%3Fid%3D7'
    );
  });

  it('מחוברת — נכנסת', () => {
    signedIn = true;
    expect(run(authGuard, '/app/dashboard')).toBe(true);
  });

  it('roleGuard מכניס את מי שיש לה אחד התפקידים המותרים', () => {
    signedIn = true;
    roles = ['Teacher'];
    expect(run(roleGuard(['Admin', 'Teacher']), '/app/students')).toBe(true);
  });

  /** יעד שלא יהיה מותר לה גם אחרי התחברות אינו שווה זכירה — היא נשלחת הביתה. */
  it('roleGuard שולח מחוברת בתפקיד אחר לעמוד הבית שלה', () => {
    signedIn = true;
    roles = ['Parent'];
    expect(serialize(run(roleGuard(['Teacher']), '/app/students'))).toBe('/parent');
  });

  it('roleGuard שולח את מי שאינה מחוברת להתחברות, וזוכר את היעד', () => {
    expect(serialize(run(roleGuard(['Teacher']), '/app/students'))).toBe('/login?returnUrl=%2Fapp%2Fstudents');
  });
});
