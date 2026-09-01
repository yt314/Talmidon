import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateStudentResourceRequest, PortalResource, StudentResource } from './resources.models';

/** חומרי לימוד — ניהול למורה, קריאה בלבד לפורטל ההורה והתלמיד. */
@Injectable({ providedIn: 'root' })
export class ResourcesService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  // ----- מורה -----

  listForStudent(studentId: string): Observable<StudentResource[]> {
    return this.http.get<StudentResource[]>(`${this.base}/students/${studentId}/resources`);
  }

  create(studentId: string, request: CreateStudentResourceRequest): Observable<StudentResource> {
    return this.http.post<StudentResource>(`${this.base}/students/${studentId}/resources`, request);
  }

  delete(studentId: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/students/${studentId}/resources/${id}`);
  }

  // ----- פורטלים -----

  /** חומרי הלימוד של התלמיד המחובר. */
  myResources(): Observable<PortalResource[]> {
    return this.http.get<PortalResource[]>(`${this.base}/resources/my-resources`);
  }

  /** חומרי הלימוד של ילדי ההורה המחובר, עם סינון אופציונלי לילד מסוים. */
  myChildrensResources(studentId?: string): Observable<PortalResource[]> {
    const params = studentId ? new HttpParams().set('studentId', studentId) : undefined;
    return this.http.get<PortalResource[]>(`${this.base}/resources/mine`, { params });
  }
}
