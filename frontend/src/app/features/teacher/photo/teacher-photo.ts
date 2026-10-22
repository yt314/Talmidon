import { signal } from '@angular/core';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { cropToSquareJpeg } from '../../../shared/avatar/image-resize.util';
import { teacherPhotoUrl } from '../../../shared/avatar/photo-url.util';
import { TeacherProfileService } from '../profile/profile.service';

/**
 * תמונת הפרופיל של המורה, כמו שהיא נערכת במסך ההקמה ובהגדרות הפרופיל: הכתובת להצגה,
 * מצב "עובד", והודעת שגיאה. משותף לשני המסכים, שבהם הקוד הזה היה כתוב פעמיים.
 *
 * התמונה נחתכת לריבוע ומוקטנת בדפדפן לפני ההעלאה, כדי שהשרת לא יצטרך ספריית עיבוד
 * תמונה ושמה שנשמר יהיה כבר בגודל התצוגה.
 */
export class TeacherPhoto {
  readonly url = signal<string | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  private teacherId = '';

  constructor(
    private readonly profileService: TeacherProfileService,
    private readonly onUploaded: () => void = () => undefined
  ) {}

  show(teacherId: string, photoVersion: number | null): void {
    this.teacherId = teacherId;
    this.url.set(teacherPhotoUrl(teacherId, photoVersion));
  }

  async upload(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    // מאפסים מיד כדי שבחירה חוזרת של אותו קובץ תפעיל את האירוע שוב
    input.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      this.error.set('יש לבחור קובץ תמונה.');
      return;
    }

    this.error.set(null);
    this.busy.set(true);
    try {
      const square = await cropToSquareJpeg(file);
      this.profileService.uploadPhoto(square).subscribe({
        next: result => {
          this.busy.set(false);
          this.url.set(teacherPhotoUrl(this.teacherId, result.photoVersion));
          this.onUploaded();
        },
        error: err => this.fail(err, 'העלאת התמונה נכשלה.')
      });
    } catch {
      this.busy.set(false);
      this.error.set('לא הצלחנו לקרוא את הקובץ. נסי תמונה אחרת.');
    }
  }

  remove(): void {
    this.busy.set(true);
    this.profileService.deletePhoto().subscribe({
      next: () => {
        this.busy.set(false);
        this.url.set(null);
      },
      error: err => this.fail(err, 'מחיקת התמונה נכשלה.')
    });
  }

  private fail(err: unknown, fallback: string): void {
    this.busy.set(false);
    this.error.set(extractErrorMessage(err, fallback));
  }
}
