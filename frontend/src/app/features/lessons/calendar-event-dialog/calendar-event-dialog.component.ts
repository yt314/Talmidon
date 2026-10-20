import { Component, inject, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService, PrimeTemplate } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { endAfterStartValidator } from '../../../core/forms/validators';
import { CalendarEventItem } from '../calendar-events.models';
import { CalendarEventsService } from '../calendar-events.service';
import { eventToFormValue, formValueToEventRequest, newEventFormValue } from '../lesson-scheduling.util';

/** חלון הוספה/עריכה של אירוע ביומן שאינו שיעור — פגישה, חופשה, יום חסום. */
@Component({
  selector: 'app-calendar-event-dialog',
  imports: [ReactiveFormsModule, PrimeTemplate, ButtonModule, CheckboxModule, DatePickerModule, DialogModule, InputTextModule, TextareaModule],
  templateUrl: './calendar-event-dialog.component.html'
})
export class CalendarEventDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly calendarEventsService = inject(CalendarEventsService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  /** נשמר או נמחק אירוע — היומן צריך לטעון מחדש. */
  readonly changed = output<void>();

  protected readonly visible = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly form = this.fb.nonNullable.group(
    {
      title: ['', [Validators.required, Validators.maxLength(200)]],
      date: this.fb.control<Date | null>(null, Validators.required),
      endDate: this.fb.control<Date | null>(null),
      startTime: this.fb.control<Date | null>(null),
      endTime: this.fb.control<Date | null>(null),
      isAllDay: [false],
      notes: ['', [Validators.maxLength(2000)]]
    },
    // בדיקת סדר השעות רלוונטית רק לאירוע עם שעות; ביום שלם השדות ריקים ממילא
    {
      validators: group =>
        group.get('isAllDay')!.value ? null : endAfterStartValidator('startTime', 'endTime')(group)
    }
  );

  /** ריק להוספה, או מלא לעריכה. */
  open(item?: CalendarEventItem): void {
    this.editingId.set(item?.id ?? null);
    this.form.reset(item ? eventToFormValue(item) : newEventFormValue(new Date()));
    this.visible.set(true);
  }

  protected save(): void {
    const request = this.form.valid ? formValueToEventRequest(this.form.getRawValue()) : null;
    if (!request) {
      this.form.markAllAsTouched();
      return;
    }

    const id = this.editingId();
    const call = id ? this.calendarEventsService.update(id, request) : this.calendarEventsService.create(request);

    this.saving.set(true);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.visible.set(false);
        this.changed.emit();
        this.messageService.add({ severity: 'success', summary: id ? 'האירוע עודכן' : 'האירוע נוסף' });
      },
      error: err => {
        this.saving.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'שמירת האירוע נכשלה.') });
      }
    });
  }

  protected remove(): void {
    const id = this.editingId();
    if (!id) return;

    this.confirmationService.confirm({
      header: 'מחיקת אירוע',
      message: 'למחוק את האירוע מהיומן?',
      acceptLabel: 'מחיקה',
      rejectLabel: 'ביטול',
      accept: () => this.doRemove(id)
    });
  }

  private doRemove(id: string): void {
    this.calendarEventsService.remove(id).subscribe({
      next: () => {
        this.visible.set(false);
        this.changed.emit();
        this.messageService.add({ severity: 'success', summary: 'האירוע נמחק' });
      },
      error: err =>
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'מחיקת האירוע נכשלה.') })
    });
  }
}
