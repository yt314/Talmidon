import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/auth/auth.service';
import { AvailabilityWindow, Subject, TeacherProfile, UpdateTeacherProfileRequest } from './profile.models';

@Injectable({ providedIn: 'root' })
export class TeacherProfileService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly api = `${environment.apiUrl}/teachers/me`;

  /**
   * The profile as last fetched, held per signed-in account.
   *
   * Three places want it on a single page load — the setup guard, the shell
   * (for the name in the header) and the dashboard — so the same request went
   * out three times. They now share one.
   *
   * Keyed by the account rather than simply cleared on logout: whoever signs
   * in next gets their own profile even if the sign-out never ran, and there
   * is no logout path left to remember to wire this into.
   */
  private cached: { account: string | null; profile$: Observable<TeacherProfile> } | null = null;

  getMyProfile(): Observable<TeacherProfile> {
    const account = this.auth.currentEmail();
    if (!this.cached || this.cached.account !== account) {
      this.cached = {
        account,
        profile$: this.http.get<TeacherProfile>(this.api).pipe(
          // A failure must not be replayed to every later caller as though it
          // were the answer — drop the cache and let the next one try again.
          catchError(error => {
            this.cached = null;
            return throwError(() => error);
          }),
          shareReplay({ bufferSize: 1, refCount: false })
        )
      };
    }
    return this.cached.profile$;
  }

  /** Anything that changes the profile makes the held copy wrong. */
  private invalidate(): void {
    this.cached = null;
  }

  updateMyProfile(request: UpdateTeacherProfileRequest): Observable<void> {
    return this.http.put<void>(this.api, request).pipe(tap(() => this.invalidate()));
  }

  /** מחליף את רשימת התחומים כולה — הממשק עורך אותה כמכלול לפני שמירה. */
  setSubjects(names: string[]): Observable<Subject[]> {
    return this.http.put<Subject[]>(`${this.api}/subjects`, { names }).pipe(tap(() => this.invalidate()));
  }

  /** הצעות להשלמה אוטומטית. אינן רשימה סגורה — אפשר להזין כל תחום. */
  subjectSuggestions(): Observable<string[]> {
    return this.http.get<string[]>(`${environment.apiUrl}/teachers/subject-suggestions`);
  }

  uploadPhoto(file: Blob): Observable<{ photoVersion: number }> {
    const form = new FormData();
    form.append('file', file, 'profile.jpg');
    return this.http
      .post<{ photoVersion: number }>(`${this.api}/photo`, form)
      .pipe(tap(() => this.invalidate()));
  }

  deletePhoto(): Observable<void> {
    return this.http.delete<void>(`${this.api}/photo`).pipe(tap(() => this.invalidate()));
  }

  getAvailability(): Observable<AvailabilityWindow[]> {
    return this.http.get<AvailabilityWindow[]>(`${this.api}/availability`);
  }

  updateAvailability(windows: AvailabilityWindow[]): Observable<void> {
    return this.http.put<void>(`${this.api}/availability`, { windows });
  }
}
