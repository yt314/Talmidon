import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { fieldError, isInvalid } from '../../core/forms/validation-messages';
import { extractErrorMessage } from '../../core/http/extract-error-message';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { AiService } from './ai.service';

/**
 * בניית מערך שיעור. הטופס קצר בכוונה — ככל שנדרש למלא יותר, כך פוחת הסיכוי שמורה
 * עסוקה תשתמש בזה בכלל.
 *
 * התוצאה מוצגת כטקסט לעריכה ולא כתצוגה בלבד: מערך שנוצר אוטומטית הוא טיוטה, והמורה
 * צריכה לתקן אותו לפני שהיא מלמדת ממנו.
 */
@Component({
  selector: 'app-lesson-plan',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    InputNumberModule,
    InputTextModule,
    TextareaModule,
    EmptyStateComponent,
    PageHeaderComponent
  ],
  templateUrl: './lesson-plan.component.html'
})
export class LessonPlanComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly ai = inject(AiService);
  private readonly messageService = inject(MessageService);
  private readonly document = inject(DOCUMENT);

  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;
  protected readonly available = this.ai.lessonPlannerAvailable;
  protected readonly provider = this.ai.provider;

  protected readonly building = signal(false);
  protected readonly plan = signal<string>('');
  /**
   * השגיאה האחרונה. מוצגת במסך ולא רק כהודעה חולפת — הודעה שנעלמת לפני שקוראים אותה
   * אינה מסבירה דבר, וגם אי אפשר לדווח עליה.
   */
  protected readonly error = signal<string | null>(null);
  /**
   * שניות ההמתנה. המסך אמר "זה לוקח כמה שניות" גם כשזה לקח דקה, וזה גרוע יותר
   * מלהמתין: מי שממתין בלי לדעת כמה מניחה שנתקע.
   */
  protected readonly elapsed = signal(0);
  private readonly destroyRef = inject(DestroyRef);
  private ticker?: ReturnType<typeof setInterval>;

  protected readonly form = this.fb.nonNullable.group({
    subject: ['', [Validators.required, Validators.maxLength(100)]],
    topic: ['', [Validators.required, Validators.maxLength(300)]],
    durationMinutes: [60, [Validators.required, Validators.min(15), Validators.max(240)]],
    gradeLevel: ['', [Validators.maxLength(100)]],
    notes: ['', [Validators.maxLength(1000)]]
  });

  ngOnInit(): void {
    this.ai.loadAvailability();
  }

  protected build(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.building.set(true);
    this.error.set(null);
    this.startTicking();
    this.ai
      .buildLessonPlan({
        subject: raw.subject.trim(),
        topic: raw.topic.trim(),
        durationMinutes: raw.durationMinutes,
        gradeLevel: raw.gradeLevel.trim() || null,
        notes: raw.notes.trim() || null
      })
      .subscribe({
        next: result => {
          this.stopTicking();
          this.building.set(false);
          this.plan.set(result.plan);
        },
        error: err => {
          const message = extractErrorMessage(err, 'בניית המערך נכשלה.');
          this.stopTicking();
          this.building.set(false);
          this.error.set(message);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: message });
        }
      });
  }

  private startTicking(): void {
    this.elapsed.set(0);
    this.ticker = setInterval(() => this.elapsed.update(seconds => seconds + 1), 1000);
    // עזיבת המסך באמצע בנייה השאירה מונה שרץ לנצח
    this.destroyRef.onDestroy(() => this.stopTicking());
  }

  private stopTicking(): void {
    if (this.ticker) clearInterval(this.ticker);
    this.ticker = undefined;
  }

  protected copy(): void {
    const text = this.plan();
    if (!text) return;

    navigator.clipboard.writeText(text).then(
      () => this.messageService.add({ severity: 'success', summary: 'המערך הועתק' }),
      () => this.messageService.add({ severity: 'warn', summary: 'ההעתקה נכשלה', detail: 'אפשר לסמן ולהעתיק ידנית.' })
    );
  }

  /** הורדה כקובץ טקסט, למי שמעדיפה להדפיס או לשמור. */
  protected download(): void {
    const text = this.plan();
    if (!text) return;

    const blob = new Blob([`﻿${text}`], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = this.document.createElement('a');
    link.href = url;
    link.download = `מערך-שיעור-${this.form.getRawValue().topic.trim() || 'ללא-נושא'}.txt`;
    link.click();
    URL.revokeObjectURL(url);
  }

  protected reset(): void {
    this.plan.set('');
    this.error.set(null);
  }
}
