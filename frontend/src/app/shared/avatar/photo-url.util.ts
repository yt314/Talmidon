import { environment } from '../../../environments/environment';

/**
 * בונה את כתובת תמונת הפרופיל מול ה-API.
 *
 * השרת מחזיר חותם גרסה בלבד ולא נתיב: נתיב מוחלט כמו "/api/..." היה נפתר מול
 * מקור הפרונטאנד, ובפיתוח הפרונט וה-API יושבים על פורטים שונים. ה-v מבטיח
 * שהחלפת תמונה תעקוף את מטמון הדפדפן.
 */
export function teacherPhotoUrl(teacherId: string, photoVersion: number | null | undefined): string | null {
  return photoVersion == null ? null : `${environment.apiUrl}/public/teachers/${teacherId}/photo?v=${photoVersion}`;
}
