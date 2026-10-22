import { TestBed } from '@angular/core/testing';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LessonAmountSuggestion } from '../lesson-pricing.util';
import { Lesson, LessonStatus } from '../lessons.models';
import { LessonsService } from '../lessons.service';
import { CompleteLessonDialogComponent } from './complete-lesson-dialog.component';

function lesson(id: string): Lesson {
  return {
    id, studentId: 's1', studentName: 'דנה', startTime: '2026-03-10T14:00:00Z', endTime: '2026-03-10T14:45:00Z',
    status: LessonStatus.Scheduled, homework: null, paymentRequired: false, amount: 0, isPaid: false, completedAt: null, seriesId: null
  } as Lesson;
}

const suggestion: LessonAmountSuggestion = { amount: 150, durationMinutes: 45, source: 'exact' };

describe('CompleteLessonDialogComponent', () => {
  let component: CompleteLessonDialogComponent;
  let complete: ReturnType<typeof vi.fn>;
  let saved: number;

  beforeEach(() => {
    complete = vi.fn(() => of(lesson('x')));
    TestBed.configureTestingModule({
      imports: [CompleteLessonDialogComponent],
      providers: [MessageService, { provide: LessonsService, useValue: { complete } }]
    });
    const fixture = TestBed.createComponent(CompleteLessonDialogComponent);
    fixture.componentRef.setInput('suggestAmount', () => suggestion);
    component = fixture.componentInstance;
    saved = 0;
    component.saved.subscribe(() => saved++);
  });

  const form = () => component['form'];

  it('נפתח עם הסכום המוצע ו"נדרש תשלום" מסומן, ומסביר מאיפה הגיע הסכום', () => {
    component.open(lesson('a'));

    expect(form().getRawValue()).toMatchObject({ completed: true, paymentRequired: true, amount: 150 });
    expect(component['amountExplanation']()).toBe('לפי מחירון השיעורים שלך ל-45 דק׳.');
  });

  it('שומר, מודיע להורה הרכיב ונסגר', () => {
    component.open(lesson('a'));
    form().patchValue({ homework: 'עמ׳ 12' });

    component['save']();

    expect(complete).toHaveBeenCalledWith('a', expect.objectContaining({ completed: true, amount: 150, homework: 'עמ׳ 12', noteContent: null }));
    expect(saved).toBe(1);
    expect(component['visible']()).toBe(false);
  });

  it('לא שולח שיעור בתשלום בלי סכום', () => {
    component.open(lesson('a'));
    form().patchValue({ amount: 0 });

    component['save']();

    expect(complete).not.toHaveBeenCalled();
  });

  it('במצב "סמן את הבא" עובר לשיעור הבא במקום להיסגר, ומדלג על מה שנשמר', () => {
    const fixture = TestBed.createComponent(CompleteLessonDialogComponent);
    fixture.componentRef.setInput('suggestAmount', () => suggestion);
    fixture.componentRef.setInput('pending', [lesson('a'), lesson('b')]);
    const dialog = fixture.componentInstance;

    dialog.open(lesson('a'), true);
    dialog['save']();

    expect(dialog['lesson']()?.id).toBe('b');
    expect(dialog['visible']()).toBe(true);
  });

  it('הערה שגלויה לתלמיד מסמנת גם "גלוי להורה" — כמו שהשרת שומר אותה', () => {
    component.open(lesson('a'));

    component['onNoteVisibleToStudentChange'](true);

    expect(form().controls.noteVisibleToParent.value).toBe(true);
  });
});
