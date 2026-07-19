import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreatePaymentRequest, OpenCharge, Payment, PaymentDetail } from './payments.models';

@Injectable({ providedIn: 'root' })
export class PaymentsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/payments`;

  openCharges(parentId: string): Observable<OpenCharge[]> {
    const params = new HttpParams().set('parentId', parentId);
    return this.http.get<OpenCharge[]>(`${this.api}/open-charges`, { params });
  }

  list(): Observable<Payment[]> {
    return this.http.get<Payment[]>(this.api);
  }

  getById(id: string): Observable<PaymentDetail> {
    return this.http.get<PaymentDetail>(`${this.api}/${id}`);
  }

  create(request: CreatePaymentRequest): Observable<Payment> {
    return this.http.post<Payment>(this.api, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/${id}`);
  }

  sendMonthlyReminders(): Observable<{ sentCount: number }> {
    return this.http.post<{ sentCount: number }>(`${this.api}/send-monthly-reminders`, {});
  }
}
