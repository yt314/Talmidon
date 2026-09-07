import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminSubjectSuggestion, AdminTeacher } from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/admin`;

  listTeachers(): Observable<AdminTeacher[]> {
    return this.http.get<AdminTeacher[]>(`${this.api}/teachers`);
  }

  lockTeacher(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/teachers/${id}/lock`, {});
  }

  unlockTeacher(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/teachers/${id}/unlock`, {});
  }

  listSubjectSuggestions(): Observable<AdminSubjectSuggestion[]> {
    return this.http.get<AdminSubjectSuggestion[]>(`${this.api}/subject-suggestions`);
  }

  addSubjectSuggestion(name: string): Observable<void> {
    return this.http.post<void>(`${this.api}/subject-suggestions`, { name });
  }

  hideSubjectSuggestion(name: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/subject-suggestions`, { params: { name } });
  }
}
