import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AiAvailability, BuildLessonPlanRequest, LessonPlanResponse } from './ai.models';

@Injectable({ providedIn: 'root' })
export class AiService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/ai`;

  /** null = עדיין לא נבדק. נשמר כדי לא לשאול את השרת בכל פתיחת מסך. */
  private readonly availability = signal<AiAvailability | null>(null);
  readonly lessonPlannerAvailable = signal(false);

  loadAvailability(): void {
    if (this.availability() !== null) return;
    this.http.get<AiAvailability>(`${this.api}/availability`).subscribe({
      next: value => {
        this.availability.set(value);
        this.lessonPlannerAvailable.set(value.lessonPlanner);
      },
      // תקלה ברשת אינה סיבה להסתיר את הכפתור לתמיד — פשוט לא נדע, וננסה שוב בכניסה הבאה
      error: () => undefined
    });
  }

  buildLessonPlan(request: BuildLessonPlanRequest): Observable<LessonPlanResponse> {
    return this.http.post<LessonPlanResponse>(`${this.api}/lesson-plan`, request);
  }
}
