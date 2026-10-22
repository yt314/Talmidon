import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';
import { LessonAmountSuggestion } from '../lesson-pricing.util';
import { amountExplanation, amountRequiredWhenChargingValidator } from '../lesson-scheduling.util';
import { Lesson } from '../lessons.models';
import { LessonsService } from '../lessons.service';

/**
 * T5 — סיום שיעור: התקיים או בוטל, תשלום, שיעורי בית והערה פדגוגית.
 *
 * נפתח לשיעור בודד, או במצב "סמן את הבא" — ואז אחרי כל שמירה עובר לשיעור הבא
 * שממתין לסימון במקום להיסגר.
 */
@Component({
  selector: 'app-complete-lesson-dialog',
  imports: [ReactiveFormsModule, ButtonModule, CheckboxModule, DialogModule, InputNumberModule, TextareaModule, IsraelDatePipe],
  templateUrl: './complete-lesson-dialog.component.html'
})
export class CompleteLessonDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly lessonsService = inject(LessonsService);
  private readonly messageService = inject(MessageService);

  /** השיעורים שממתינים לסימון, מהמוקדם — עבור מצב "סמן את הבא". */
  readonly pending = input<Lesson[]>([]);
  /** הסכום המוצע לשיעור, לפי המשך שלו בפועל ומחירוני המורה והתלמיד. */
  readonly suggestAmount = input.required<(lesson: Lesson) => LessonAmountSuggestion>();
  readonly saved = output<void>();

  protected readonly visible = signal(false);
  /** השיעור שבדיאלוג — כדי שהמורה תראה על מה בדיוק היא מסמנת. */
  protected readonly lesson = signal<Lesson | null>(null);
  /** ההצעה שחושבה בפתיחה — משמשת להסבר שמתחת לשדה הסכום. */
  private readonly suggestion = signal<LessonAmountSuggestion | null>(null);
  protected readonly saving = signal(false);
  /** true כשהסימון התחיל מ"סמן את הבא" — אז ממשיכים לשיעור הבא במקום לסגור. */
  protected readonly batch = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly amountExplanation = computed(() => {
    const suggestion = this.suggestion();
    return suggestion ? amountExplanation(suggestion) : null;
  });

  /** כמה עוד ממתינים לסימון אחרי זה שבדיאלוג. השיעור הנוכחי עדיין "מתוזמן", ולכן נספר. */
  protected readonly remainingAfterCurrent = computed(() => Math.max(this.pending().length - 1, 0));

  protected readonly form = this.fb.nonNullable.group(
    {
      completed: [true],
      paymentRequired: [false],
      amount: [0, [Validators.min(0)]],
      homework: ['', [Validators.maxLength(2000)]],
      noteContent: ['', [Validators.maxLength(4000)]],
      noteVisibleToStudent: [false],
      noteVisibleToParent: [false]
    },
    { validators: amountRequiredWhenChargingValidator }
  );

  open(lesson: Lesson, batch = false): void {
    this.batch.set(batch);
    this.lesson.set(lesson);
    const suggestion = this.suggestAmount()(lesson);
    this.suggestion.set(suggestion);
    this.form.reset({
      completed: true,
      // שיעור שהתקיים הוא שיעור שגובים עליו — זה המקרה הרגיל, והמורה מורידה את
      // הסימון בחריגים (שיעור ניסיון, השלמה ללא תשלום).
      paymentRequired: true,
      // מחושב מהמשך בפועל של השיעור ומטבלת המחירים; ניתן לעריכה בשדה עצמו.
      amount: suggestion.amount,
      homework: '',
      noteContent: '',
      noteVisibleToStudent: false,
      noteVisibleToParent: false
    });
    this.visible.set(true);
  }

  /**
   * הערה שגלויה לתלמידה גלויה תמיד גם להורה — כלל שנאכף בשרת, ובצדק: אין להסתיר
   * מהורה מה שנכתב על ילדו. מה שלא היה כאן הוא שהמסך יאמר את זה: אפשר היה לסמן
   * "גלוי לתלמיד" בלבד, לשמור, ולחשוב שנכתב משהו שההורה לא יקרא — בזמן שהוא כבר
   * גלוי לו. מסמנים גם את התיבה השנייה, כדי שמה שנראה יהיה מה שנשמר.
   */
  protected onNoteVisibleToStudentChange(checked: boolean): void {
    if (checked) this.form.controls.noteVisibleToParent.setValue(true);
  }

  protected onHidden(): void {
    this.batch.set(false);
  }

  protected save(): void {
    const id = this.lesson()?.id;
    if (!id) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    this.saving.set(true);
    this.lessonsService
      .complete(id, {
        completed: raw.completed,
        paymentRequired: raw.paymentRequired,
        amount: raw.amount,
        homework: raw.homework || null,
        noteContent: raw.noteContent || null,
        noteVisibleToStudent: raw.noteVisibleToStudent,
        noteVisibleToParent: raw.noteVisibleToParent
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({ severity: 'success', summary: 'השיעור עודכן' });
          this.saved.emit();
          this.continueOrClose(id);
        },
        error: err => {
          this.saving.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'העדכון נכשל.') });
        }
      });
  }

  /**
   * "סמן את הבא" נלחץ כדי לעבור על כולם. סגירה אחרי כל אחד הייתה מחזירה לבאנר
   * ומחייבת פתיחה מחדש — בשבוע טיפוסי כאן חמישה שיעורים. הרשימה עדיין זו שלפני
   * הטעינה מחדש, ולכן מדלגים על מה שהרגע נשמר.
   */
  private continueOrClose(savedId: string): void {
    const next = this.batch() ? this.pending().find(l => l.id !== savedId) : undefined;
    if (next) this.open(next, true);
    else this.visible.set(false);
  }
}
