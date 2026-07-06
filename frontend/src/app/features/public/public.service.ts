import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PublicTeacherDetail, PublicTeacherSummary } from './public.models';

@Injectable({ providedIn: 'root' })
export class PublicService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/public/teachers`;

  listTeachers(): Observable<PublicTeacherSummary[]> {
    return this.http.get<PublicTeacherSummary[]>(this.api);
  }

  listSubjects(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/subjects`);
  }

  getTeacher(id: string): Observable<PublicTeacherDetail> {
    return this.http.get<PublicTeacherDetail>(`${this.api}/${id}`);
  }
}
