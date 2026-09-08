import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ChatMessage, MessageRecipient, ThreadDetail, ThreadSummary, UnreadCount } from './messages.models';

/**
 * שיחות בין המורה לתלמידות ולהורים.
 *
 * שני קבוצות נתיבים לאותו מנגנון: הצד של המורה, והצד של מי שמדבר איתה. התלמידה וההורה
 * חולקים את אותם נתיבים ("mine") — השרת יודע מי מחובר, ולכן אין צורך בשני ממשקים.
 */
@Injectable({ providedIn: 'root' })
export class MessagesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/messages`;

  // ----- מורה -----

  inbox(includeClosed = true): Observable<ThreadSummary[]> {
    return this.http.get<ThreadSummary[]>(`${this.base}?includeClosed=${includeClosed}`);
  }

  teacherUnreadCount(): Observable<UnreadCount> {
    return this.http.get<UnreadCount>(`${this.base}/unread-count`);
  }

  recipients(): Observable<MessageRecipient[]> {
    return this.http.get<MessageRecipient[]>(`${this.base}/recipients`);
  }

  thread(id: string): Observable<ThreadDetail> {
    return this.http.get<ThreadDetail>(`${this.base}/${id}`);
  }

  startThread(request: {
    studentId: string;
    counterpartRole: number | null;
    counterpartId: string | null;
    subject: string;
    body: string;
  }): Observable<ThreadDetail> {
    return this.http.post<ThreadDetail>(this.base, request);
  }

  teacherReply(id: string, body: string): Observable<ChatMessage> {
    return this.http.post<ChatMessage>(`${this.base}/${id}/reply`, { body });
  }

  setClosed(id: string, closed: boolean): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/${closed ? 'close' : 'reopen'}`, {});
  }

  // ----- תלמידה והורה -----

  myThreads(): Observable<ThreadSummary[]> {
    return this.http.get<ThreadSummary[]>(`${this.base}/mine`);
  }

  myUnreadCount(): Observable<UnreadCount> {
    return this.http.get<UnreadCount>(`${this.base}/mine/unread-count`);
  }

  myThread(id: string): Observable<ThreadDetail> {
    return this.http.get<ThreadDetail>(`${this.base}/mine/${id}`);
  }

  /** תלמידה אינה שולחת studentId — היא פונה בעניין עצמה. הורה חייב לציין באיזה ילד מדובר. */
  startMyThread(request: {
    studentId: string | null;
    relatedNoteId: string | null;
    subject: string;
    body: string;
  }): Observable<ThreadDetail> {
    return this.http.post<ThreadDetail>(`${this.base}/mine`, request);
  }

  myReply(id: string, body: string): Observable<ChatMessage> {
    return this.http.post<ChatMessage>(`${this.base}/mine/${id}/reply`, { body });
  }
}
