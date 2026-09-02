import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ContactRequest, ContactRequestStatus, CreateContactRequest } from './contact-requests.models';

@Injectable({ providedIn: 'root' })
export class ContactRequestsService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  /** שליחה מהספרייה הציבורית — ללא התחברות. */
  send(teacherId: string, request: CreateContactRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/public/teachers/${teacherId}/contact`, request);
  }

  list(status?: ContactRequestStatus): Observable<ContactRequest[]> {
    const params = status !== undefined ? new HttpParams().set('status', status) : undefined;
    return this.http.get<ContactRequest[]>(`${this.base}/contact-requests`, { params });
  }

  newCount(): Observable<number> {
    return this.http.get<number>(`${this.base}/contact-requests/new-count`);
  }

  updateStatus(id: string, status: ContactRequestStatus): Observable<void> {
    return this.http.put<void>(`${this.base}/contact-requests/${id}/status`, { status });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/contact-requests/${id}`);
  }
}
