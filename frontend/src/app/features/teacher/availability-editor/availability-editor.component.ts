import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { HEBREW_DAY_NAMES } from '../../../core/i18n/primeng-hebrew';
import { AvailabilityWindow } from '../profile/profile.models';
import { TeacherProfileService } from '../profile/profile.service';

/**
 * שעות הזמינות השבועיות של המורה. מודגשות ביומן, ומתריעות בעת קביעת שיעור מחוצה להן.
 * אפשר כמה חלונות ליום (בוקר וערב); הכל נשמר יחד, בכפתור אחד.
 */
@Component({
  selector: 'app-availability-editor',
  imports: [FormsModule, ButtonModule, CardModule, InputTextModule],
  templateUrl: './availability-editor.component.html',
  host: { class: 'block' }
})
export class AvailabilityEditorComponent implements OnInit {
  private readonly profileService = inject(TeacherProfileService);
  private readonly messageService = inject(MessageService);

  protected readonly dayNames = HEBREW_DAY_NAMES;
  protected readonly days = [0, 1, 2, 3, 4, 5, 6];
  protected readonly windows = signal<AvailabilityWindow[]>([]);
  protected readonly saving = signal(false);

  ngOnInit(): void {
    this.profileService.getAvailability().subscribe(windows => this.windows.set(windows));
  }

  protected windowsForDay(day: number): AvailabilityWindow[] {
    return this.windows().filter(w => w.dayOfWeek === day);
  }

  protected addWindow(day: number): void {
    this.windows.set([...this.windows(), { dayOfWeek: day, startTime: '09:00', endTime: '10:00' }]);
  }

  protected removeWindow(win: AvailabilityWindow): void {
    this.windows.set(this.windows().filter(w => w !== win));
  }

  protected save(): void {
    if (this.windows().some(w => w.endTime <= w.startTime)) {
      this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: 'בכל חלון, שעת הסיום חייבת להיות אחרי שעת ההתחלה.' });
      return;
    }
    this.saving.set(true);
    this.profileService.updateAvailability(this.windows()).subscribe({
      next: () => {
        this.saving.set(false);
        this.messageService.add({ severity: 'success', summary: 'שעות הזמינות נשמרו' });
      },
      error: err => {
        this.saving.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'השמירה נכשלה.') });
      }
    });
  }
}
