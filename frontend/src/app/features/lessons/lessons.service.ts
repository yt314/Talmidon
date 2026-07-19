import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ChangeRequest,
  ChangeRequestStatus,
  CompleteLessonRequest,
  CreateLessonRequest,
  Lesson,
  UpdateLessonRequest
} from './lessons.models';

@Injectable({ providedIn: 'root' })
export class LessonsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/lessons`;

  list(from?: Date, to?: Date): Observable<Lesson[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from.toISOString());
    if (to) params = params.set('to', to.toISOString());
    return this.http.get<Lesson[]>(this.api, { params });
  }

  create(request: CreateLessonRequest): Observable<Lesson> {
    return this.http.post<Lesson>(this.api, request);
  }

  update(id: string, request: UpdateLessonRequest): Observable<void> {
    return this.http.put<void>(`${this.api}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/${id}`);
  }

  complete(id: string, request: CompleteLessonRequest): Observable<Lesson> {
    return this.http.post<Lesson>(`${this.api}/${id}/complete`, request);
  }

  approveRequest(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/${id}/approve`, {});
  }

  declineRequest(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/${id}/decline`, {});
  }

  listChangeRequests(status?: ChangeRequestStatus): Observable<ChangeRequest[]> {
    let params = new HttpParams();
    if (status !== undefined) params = params.set('status', status);
    return this.http.get<ChangeRequest[]>(`${this.api}/change-requests`, { params });
  }

  approveChangeRequest(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/change-requests/${id}/approve`, {});
  }

  rejectChangeRequest(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/change-requests/${id}/reject`, {});
  }
}
