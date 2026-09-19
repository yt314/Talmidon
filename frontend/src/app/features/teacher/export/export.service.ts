import { HttpClient, HttpResponse } from '@angular/common/http';
import { DOCUMENT, Injectable, inject } from '@angular/core';
import { environment } from '../../../../environments/environment';

export type ExportKind = 'students' | 'lessons' | 'payments';

/**
 * הורדת הנתונים כקובץ.
 *
 * דרך fetch ולא כקישור רגיל: הנתיב דורש הזדהות, ולקישור בדפדפן אין את אסימון הגישה.
 * הקובץ מגיע כ-blob ונשמר בשם שהשרת נקב בו.
 */
@Injectable({ providedIn: 'root' })
export class DataExportService {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);

  download(kind: ExportKind): Promise<void> {
    return new Promise((resolve, reject) => {
      this.http
        .get(`${environment.apiUrl}/export/${kind}.csv`, { observe: 'response', responseType: 'blob' })
        .subscribe({
          next: response => {
            this.save(response, kind);
            resolve();
          },
          error: reject
        });
    });
  }

  private save(response: HttpResponse<Blob>, kind: ExportKind): void {
    const url = URL.createObjectURL(response.body!);
    const link = this.document.createElement('a');
    link.href = url;
    link.download = this.fileName(response, kind);
    link.click();
    URL.revokeObjectURL(url);
  }

  /**
   * שם הקובץ מגיע ב-Content-Disposition. מעדיפים את ‎filename*‎ המקודד — הוא זה שנושא
   * את השם בעברית; אם הוא חסר, נשארים עם שם סביר במקום "download".
   */
  private fileName(response: HttpResponse<Blob>, kind: ExportKind): string {
    const header = response.headers.get('Content-Disposition') ?? '';
    const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header)?.[1];
    if (encoded) {
      try {
        return decodeURIComponent(encoded);
      } catch {
        // כותרת פגומה אינה סיבה לא להוריד את הקובץ
      }
    }
    return `talmidon-${kind}.csv`;
  }
}
