import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { Subject, TeacherProfile, UpdateTeacherProfileRequest } from './profile.models';

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

  addSubject(name: string): Observable<Subject> {
    return this.http.post<Subject>(`${this.api}/subjects`, { name });
  }

  deleteSubject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/subjects/${id}`);
  }
}
