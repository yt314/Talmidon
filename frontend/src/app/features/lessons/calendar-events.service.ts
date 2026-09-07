import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CalendarEventItem, SaveCalendarEventRequest } from './calendar-events.models';

@Injectable({ providedIn: 'root' })
export class CalendarEventsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/calendar-events`;

  list(from?: Date, to?: Date): Observable<CalendarEventItem[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from.toISOString());
    if (to) params = params.set('to', to.toISOString());
    return this.http.get<CalendarEventItem[]>(this.api, { params });
  }

  create(request: SaveCalendarEventRequest): Observable<CalendarEventItem> {
    return this.http.post<CalendarEventItem>(this.api, request);
  }

  update(id: string, request: SaveCalendarEventRequest): Observable<CalendarEventItem> {
    return this.http.put<CalendarEventItem>(`${this.api}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/${id}`);
  }
}
