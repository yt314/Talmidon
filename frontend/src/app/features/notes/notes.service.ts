import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateNoteRequest, Note, UpdateNoteRequest } from './notes.models';

@Injectable({ providedIn: 'root' })
export class NotesService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/notes`;

  listForStudent(studentId: string): Observable<Note[]> {
    const params = new HttpParams().set('studentId', studentId);
    return this.http.get<Note[]>(this.api, { params });
  }

  create(request: CreateNoteRequest): Observable<Note> {
    return this.http.post<Note>(this.api, request);
  }

  update(id: string, request: UpdateNoteRequest): Observable<void> {
    return this.http.put<void>(`${this.api}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/${id}`);
  }
}
