import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateParentRequest, Parent, UpdateParentRequest } from './parents.models';

@Injectable({ providedIn: 'root' })
export class ParentsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/parents`;

  list(): Observable<Parent[]> {
    return this.http.get<Parent[]>(this.api);
  }

  create(request: CreateParentRequest): Observable<Parent> {
    return this.http.post<Parent>(this.api, request);
  }

  update(id: string, request: UpdateParentRequest): Observable<void> {
    return this.http.put<void>(`${this.api}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/${id}`);
  }
}
