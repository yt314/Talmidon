import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateStudentRequest, MyStudentProfile, StudentDetail, StudentListItem, UpdateStudentRequest } from './students.models';

@Injectable({ providedIn: 'root' })
export class StudentsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/students`;

  list(): Observable<StudentListItem[]> {
    return this.http.get<StudentListItem[]>(this.api);
  }

  getById(id: string): Observable<StudentDetail> {
    return this.http.get<StudentDetail>(`${this.api}/${id}`);
  }

  create(request: CreateStudentRequest): Observable<StudentDetail> {
    return this.http.post<StudentDetail>(this.api, request);
  }

  update(id: string, request: UpdateStudentRequest): Observable<void> {
    return this.http.put<void>(`${this.api}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/${id}`);
  }

  linkParent(studentId: string, parentId: string): Observable<void> {
    return this.http.post<void>(`${this.api}/${studentId}/parents/${parentId}`, {});
  }

  unlinkParent(studentId: string, parentId: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/${studentId}/parents/${parentId}`);
  }

  myProfile(): Observable<MyStudentProfile> {
    return this.http.get<MyStudentProfile>(`${this.api}/me`);
  }
}
