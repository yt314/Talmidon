import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IncomeReport } from './reports.models';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/reports`;

  incomeReport(year: number, month: number): Observable<IncomeReport> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<IncomeReport>(`${this.api}/income`, { params });
  }
}
