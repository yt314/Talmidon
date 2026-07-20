import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MyStudentProfile } from '../students/students.models';
import { StudentLesson, StudentNote } from './student-portal.models';

@Injectable({ providedIn: 'root' })
export class StudentPortalService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  mySchedule(): Observable<StudentLesson[]> {
    return this.http.get<StudentLesson[]>(`${this.base}/lessons/my-schedule`);
  }

  myNotes(): Observable<StudentNote[]> {
    return this.http.get<StudentNote[]>(`${this.base}/notes/my-notes`);
  }

  myProfile(): Observable<MyStudentProfile> {
    return this.http.get<MyStudentProfile>(`${this.base}/students/me`);
  }
}
