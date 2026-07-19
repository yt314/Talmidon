import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ChangeRequestType, CreateLessonRequest, Lesson } from '../lessons/lessons.models';
import { OpenCharge, Payment } from '../payments/payments.models';
import { MyChild, ParentNote } from './parent-portal.models';

export interface CreateChangeRequestRequest {
  type: ChangeRequestType;
  proposedStartTime?: string | null;
  proposedEndTime?: string | null;
  reason?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ParentPortalService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  myChildren(): Observable<MyChild[]> {
    return this.http.get<MyChild[]>(`${this.base}/students/mine`);
  }

  myLessons(studentId?: string | null): Observable<Lesson[]> {
    let params = new HttpParams();
    if (studentId) params = params.set('studentId', studentId);
    return this.http.get<Lesson[]>(`${this.base}/lessons/mine`, { params });
  }

  requestLesson(request: CreateLessonRequest): Observable<Lesson> {
    return this.http.post<Lesson>(`${this.base}/lessons/requests`, request);
  }

  requestChange(lessonId: string, request: CreateChangeRequestRequest): Observable<unknown> {
    return this.http.post(`${this.base}/lessons/${lessonId}/change-requests`, request);
  }

  myNotes(studentId?: string | null): Observable<ParentNote[]> {
    let params = new HttpParams();
    if (studentId) params = params.set('studentId', studentId);
    return this.http.get<ParentNote[]>(`${this.base}/notes/mine`, { params });
  }

  myOpenCharges(): Observable<OpenCharge[]> {
    return this.http.get<OpenCharge[]>(`${this.base}/payments/mine/open-charges`);
  }

  myPayments(): Observable<Payment[]> {
    return this.http.get<Payment[]>(`${this.base}/payments/mine`);
  }
}
