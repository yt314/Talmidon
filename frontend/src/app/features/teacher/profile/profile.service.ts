import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AvailabilityWindow, Subject, TeacherProfile, UpdateTeacherProfileRequest } from './profile.models';

@Injectable({ providedIn: 'root' })
export class TeacherProfileService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/teachers/me`;

  getMyProfile(): Observable<TeacherProfile> {
    return this.http.get<TeacherProfile>(this.api);
  }

  updateMyProfile(request: UpdateTeacherProfileRequest): Observable<void> {
    return this.http.put<void>(this.api, request);
  }

  /** מחליף את רשימת התחומים כולה — הממשק עורך אותה כמכלול לפני שמירה. */
  setSubjects(names: string[]): Observable<Subject[]> {
    return this.http.put<Subject[]>(`${this.api}/subjects`, { names });
  }

  /** הצעות להשלמה אוטומטית. אינן רשימה סגורה — אפשר להזין כל תחום. */
  subjectSuggestions(): Observable<string[]> {
    return this.http.get<string[]>(`${environment.apiUrl}/teachers/subject-suggestions`);
  }

  uploadPhoto(file: Blob): Observable<{ photoVersion: number }> {
    const form = new FormData();
    form.append('file', file, 'profile.jpg');
    return this.http.post<{ photoVersion: number }>(`${this.api}/photo`, form);
  }

  deletePhoto(): Observable<void> {
    return this.http.delete<void>(`${this.api}/photo`);
  }

  getAvailability(): Observable<AvailabilityWindow[]> {
    return this.http.get<AvailabilityWindow[]>(`${this.api}/availability`);
  }

  updateAvailability(windows: AvailabilityWindow[]): Observable<void> {
    return this.http.put<void>(`${this.api}/availability`, { windows });
  }
}
