import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminError, AdminErrorDetail, AdminSiteFeedback, AdminSubjectSuggestion, AdminTeacher } from './admin.models';

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

  listFeedback(includeHandled: boolean): Observable<AdminSiteFeedback[]> {
    return this.http.get<AdminSiteFeedback[]>(`${this.api}/feedback`, {
      params: { includeHandled }
    });
  }

  markFeedbackHandled(id: string, handled: boolean): Observable<void> {
    return this.http.post<void>(`${this.api}/feedback/${id}/handled`, null, {
      params: { handled }
    });
  }

  /** `reference` הוא חיפוש לפי הקוד שמשתמשת מסרה — הוא גובר על סינון "שטופלו". */
  listErrors(includeHandled: boolean, reference: string): Observable<AdminError[]> {
    const params: Record<string, string | boolean> = reference.trim()
      ? { reference: reference.trim() }
      : { includeHandled };
    return this.http.get<AdminError[]>(`${this.api}/errors`, { params });
  }

  getError(id: string): Observable<AdminErrorDetail> {
    return this.http.get<AdminErrorDetail>(`${this.api}/errors/${id}`);
  }

  markErrorHandled(id: string, handled: boolean): Observable<void> {
    return this.http.post<void>(`${this.api}/errors/${id}/handled`, null, {
      params: { handled }
    });
  }
}
