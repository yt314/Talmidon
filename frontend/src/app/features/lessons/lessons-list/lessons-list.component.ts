import { formatDate } from '@angular/common';
import { Component, LOCALE_ID, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService, PrimeTemplate } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { endAfterStartValidator } from '../../../core/forms/validators';
import { CalendarEventDrop, CalendarEventExtendedProps, CalendarSlotSelection } from '../../../shared/calendar/lesson-calendar.model';
import { LessonCalendarComponent } from '../../../shared/calendar/lesson-calendar.component';
import { AiService } from '../../ai/ai.service';
import { StudentListItem } from '../../students/students.models';
import { StudentsService } from '../../students/students.service';
import { AvailabilityWindow } from '../../teacher/profile/profile.models';
import { TeacherProfileService } from '../../teacher/profile/profile.service';
import { HEBREW_DAY_NAMES } from '../../../core/i18n/primeng-hebrew';
import { buildTeacherCalendarEvents } from '../lesson-calendar.util';
import {
  LESSON_STATUS_LABELS,
  LESSON_STATUS_SEVERITY,
  ChangeRequest,
  ChangeRequestStatus,
  ChangeRequestType,
  Lesson,
  LessonStatus
} from '../lessons.models';
import { LessonsService } from '../lessons.service';
import { CalendarEventsService } from '../calendar-events.service';
import { CalendarEventItem } from '../calendar-events.models';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';
import { LessonAmountSuggestion, lessonDurationMinutes, suggestLessonAmountDetailed } from '../lesson-pricing.util';
import {
  combineDateTime,
  confirmAnyway,
  conflictText,
  findChangeRequestConflict,
  findConflict,
  futureOccurrencesOfSeries,
  lessonsAwaitingMark,
  nextPendingAnswer,
  preSaveWarning
} from '../lesson-scheduling.util';
import { AddLessonDialogComponent } from '../add-lesson-dialog/add-lesson-dialog.component';
import { CompleteLessonDialogComponent } from '../complete-lesson-dialog/complete-lesson-dialog.component';
import { CalendarEventDialogComponent } from '../calendar-event-dialog/calendar-event-dialog.component';

/**
 * יומן השיעורים של המורה: היומן עצמו, הבאנרים של מה שממתין לה, ופרטי שיעור/בקשה.
 * הוספת שיעור, סיום שיעור ואירוע אישי הם רכיבים נפרדים; הכללים (התנגשויות, שעות
 * זמינות, מה ממתין) ב-lesson-scheduling.util.
 */
@Component({
  selector: 'app-lessons-list',
  imports: [ ReactiveFormsModule,
    FormsModule,
    PrimeTemplate,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    MessageModule,
    SelectModule,
    TagModule,
    LessonCalendarComponent, PageHeaderComponent, IsraelDatePipe,
    AddLessonDialogComponent, CompleteLessonDialogComponent, CalendarEventDialogComponent],
  templateUrl: './lessons-list.component.html'
})
export class LessonsListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly lessonsService = inject(LessonsService);
  private readonly calendarEventsService = inject(CalendarEventsService);
  private readonly studentsService = inject(StudentsService);
  private readonly profileService = inject(TeacherProfileService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly ai = inject(AiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly locale = inject(LOCALE_ID);

  private readonly addLessonDialog = viewChild.required(AddLessonDialogComponent);
  private readonly completeDialog = viewChild.required(CompleteLessonDialogComponent);
  private readonly eventDialog = viewChild.required(CalendarEventDialogComponent);

  /** מוסתר כשאין ספק מוגדר בשרת — כפתור שמוביל למסך שמתנצל אינו כפתור. */
  protected readonly lessonPlannerAvailable = this.ai.lessonPlannerAvailable;

  /** ברירות מחדל של המורה (מחיר ומשך), למילוי אוטומטי בקביעת שיעור. */
  protected readonly teacherDefaults = signal<{
    price: number;
    pricePer45: number | null;
    pricePer30: number | null;
    durationMinutes: number;
  }>({ price: 0, pricePer45: null, pricePer30: null, durationMinutes: 60 });

  /** חלונות זמינות שבועיים של המורה — להדגשה ביומן ולאזהרה בקביעה מחוץ לשעות. */
  protected readonly availability = signal<AvailabilityWindow[]>([]);

  protected readonly businessHours = computed(() => {
    const windows = this.availability();
    // מערך ריק גורם ל-FullCalendar לצבוע את כל היומן כאפור — לכן undefined כשאין שעות מוגדרות.
    return windows.length ? windows.map(w => ({ daysOfWeek: [w.dayOfWeek], startTime: w.startTime, endTime: w.endTime })) : undefined;
  });

  /** סיכום טקסטואלי של שעות הזמינות, לתצוגה מעל היומן. */
  protected readonly availabilitySummary = computed(() => {
    return this.availability()
      .slice()
      .sort((a, b) => a.dayOfWeek - b.dayOfWeek || a.startTime.localeCompare(b.startTime))
      .map(w => `${HEBREW_DAY_NAMES[w.dayOfWeek]} ${w.startTime}–${w.endTime}`);
  });

  protected readonly LessonStatus = LessonStatus;
  protected readonly ChangeRequestType = ChangeRequestType;
  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];

  protected readonly lessons = signal<Lesson[]>([]);
  protected readonly loading = signal(true);
  protected readonly students = signal<StudentListItem[]>([]);
  protected readonly personalEvents = signal<CalendarEventItem[]>([]);
  protected readonly changeRequests = signal<ChangeRequest[]>([]);
  protected readonly changeRequestsLoading = signal(true);

  /** פתיחת שעות הזמינות בטלפון. במחשב השורה פתוחה תמיד ב-CSS. */
  protected readonly showAvailability = signal(false);

  /** סינון היומן לפי תלמיד (null = כל התלמידים). */
  protected readonly studentFilter = signal<string | null>(null);

  protected readonly calendarEvents = computed(() => {
    const filter = this.studentFilter();
    const lessons = filter ? this.lessons().filter(l => l.studentId === filter) : this.lessons();
    const requests = filter ? this.changeRequests().filter(r => r.studentId === filter) : this.changeRequests();
    return buildTeacherCalendarEvents(lessons, requests, this.personalEvents());
  });

  /** שיעורים שהמועד שלהם עבר ועדיין "מתוזמן" — ממתינים לסימון (התקיים/בוטל). */
  protected readonly pendingToMark = computed(() => lessonsAwaitingMark(this.lessons(), Date.now()));

  protected readonly showEditTimeDialog = signal(false);
  protected readonly editingLessonId = signal<string | null>(null);
  protected readonly savingTime = signal(false);

  protected readonly showLessonDetailDialog = signal(false);
  protected readonly selectedLesson = signal<Lesson | null>(null);

  protected readonly showChangeRequestDialog = signal(false);
  protected readonly selectedChangeRequest = signal<ChangeRequest | null>(null);

  /**
   * A lesson already on the calendar that clashes with the open request.
   *
   * The double-booking check existed only where the teacher types a time
   * herself. On these two screens the time was chosen by someone else — a
   * parent or a student — which is exactly where she is least likely to have
   * looked at her own calendar first, and where she used to get no warning at
   * all.
   */
  protected readonly requestedLessonConflict = computed(() => {
    const lesson = this.selectedLesson();
    if (!lesson || lesson.status !== LessonStatus.Requested) return null;
    return findConflict(this.lessons(), new Date(lesson.startTime), new Date(lesson.endTime), lesson.id);
  });

  /** The same, for a parent's proposed new time on an existing lesson. */
  protected readonly changeRequestConflict = computed(() => {
    const request = this.selectedChangeRequest();
    return request ? findChangeRequestConflict(this.lessons(), request) : null;
  });

  /**
   * הבקשות שממתינות לתשובה: בקשה לשיעור חדש, ובקשה לשינוי מועד או ביטול לשיעור
   * קיים. שתיהן יחד הן מה שלוח המחוונים סופר תחת "בקשות ממתינות".
   */
  protected readonly pendingLessonRequests = computed(() =>
    this.lessons().filter(l => l.status === LessonStatus.Requested)
  );

  protected readonly pendingChangeRequests = computed(() =>
    this.changeRequests().filter(r => r.status === ChangeRequestStatus.Pending)
  );

  protected readonly pendingAnswersCount = computed(
    () => this.pendingLessonRequests().length + this.pendingChangeRequests().length
  );

  protected readonly showCancelSeriesDialog = signal(false);
  protected readonly cancellingSeriesId = signal<string | null>(null);
  protected readonly cancelSeriesDeleteFuture = signal(false);
  protected readonly cancellingSeries = signal(false);

  protected readonly busyRequestId = signal<string | null>(null);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly timeForm = this.fb.nonNullable.group(
    {
      date: this.fb.control<Date | null>(null, Validators.required),
      startTime: this.fb.control<Date | null>(null, Validators.required),
      endTime: this.fb.control<Date | null>(null, Validators.required)
    },
    { validators: endAfterStartValidator('startTime', 'endTime') }
  );

  /**
   * הסכום המוצע לשיעור שהסתיים — לפי המשך שלו בפועל ולא לפי משך ברירת המחדל.
   * הכלל עצמו ב-suggestLessonAmount, יחד עם הבדיקות שלו.
   */
  protected readonly suggestAmount = (lesson: Lesson): LessonAmountSuggestion => {
    const student = this.students().find(s => s.id === lesson.studentId);
    const defaults = this.teacherDefaults();
    return suggestLessonAmountDetailed(
      lessonDurationMinutes(lesson.startTime, lesson.endTime),
      { pricePerHour: defaults.price, pricePer45: defaults.pricePer45, pricePer30: defaults.pricePer30 },
      { pricePerLesson: student?.defaultPricePerLesson, durationMinutes: student?.defaultDurationMinutes }
    );
  };

  ngOnInit(): void {
    this.ai.loadAvailability();
    this.loadLessons();
    this.loadChangeRequests();
    this.loadPersonalEvents();
    this.studentsService.list().subscribe(students => {
      this.students.set(students);
      this.openFromQueryParam();
    });
    this.profileService.getMyProfile().subscribe(profile =>
      this.teacherDefaults.set({
        price: profile.defaultPricePerLesson,
        pricePer45: profile.pricePer45Minutes,
        pricePer30: profile.pricePer30Minutes,
        durationMinutes: profile.defaultDurationMinutes
      }));
    this.profileService.getAvailability().subscribe(windows => this.availability.set(windows));
  }

  /** אם הגענו מכרטיס תלמיד (?studentId=...) — פתח ישר את דיאלוג ההוספה עם התלמיד נבחר. */
  private openFromQueryParam(): void {
    const studentId = this.route.snapshot.queryParamMap.get('studentId');
    if (studentId && this.students().some(s => s.id === studentId)) {
      this.addLessonDialog().open(studentId);
    }
  }

  // ===== פעולות מהכותרת ומהיומן =====

  openAddLessonDialog(): void {
    this.addLessonDialog().open();
  }

  openEventDialog(item?: CalendarEventItem): void {
    this.eventDialog().open(item);
  }

  /**
   * ניווט מפורש ולא routerLink על p-button: עם routerLink סטטי הרכיב מרונדר ריק.
   * הכפתור מוצג תמיד — המסך עצמו מסביר אם התכונה אינה מוגדרת בשרת, וזה עדיף על
   * ‎@if‎ סביב תוכן מוקרן, שבו כפתור PrimeNG (OnPush) לא עובר בדיקה ראשונה ויוצא ריק.
   */
  openLessonPlanner(): void {
    this.router.navigate(['/app/lesson-plan']);
  }

  onSlotSelected(range: CalendarSlotSelection): void {
    this.addLessonDialog().openForSlot(range);
  }

  /** גרירת שיעור ללוח (רק מתוזמן ניתן לגרירה — נאכף ב-lesson-calendar.util). מבקש אישור לפני שמירה בפועל. */
  onEventDropped(drop: CalendarEventDrop): void {
    this.onLessonTimeDragged(drop, lesson => ({
      header: 'אישור שינוי מועד',
      message: `להעביר את השיעור של ${lesson.studentName} מ-${this.format(lesson.startTime, 'dd/MM/yyyy HH:mm')} ל-${this.format(drop.start, 'dd/MM/yyyy HH:mm')}?`,
      icon: 'pi pi-calendar',
      success: 'המועד עודכן',
      failure: 'עדכון המועד נכשל.'
    }));
  }

  /** גרירת קצה השיעור לשינוי משך (רק שיעור "מתוזמן" ניתן לכך — נאכף ב-lesson-calendar.util). מבקש אישור לפני שמירה בפועל. */
  onEventResized(resize: CalendarEventDrop): void {
    this.onLessonTimeDragged(resize, lesson => ({
      header: 'אישור שינוי משך',
      message: `לשנות את משך השיעור של ${lesson.studentName} כך שיסתיים ב-${this.format(resize.end, 'HH:mm')} (במקום ${this.format(lesson.endTime, 'HH:mm')})?`,
      icon: 'pi pi-clock',
      success: 'המשך עודכן',
      failure: 'עדכון המשך נכשל.'
    }));
  }

  onEventClicked(props: CalendarEventExtendedProps): void {
    switch (props.kind) {
      case 'lesson':
      case 'request': {
        const lesson = this.lessons().find(l => l.id === props.refId);
        if (lesson) this.showLessonDetail(lesson);
        break;
      }
      case 'personal': {
        const item = this.personalEvents().find(e => e.id === props.refId);
        if (item) this.openEventDialog(item);
        break;
      }
      case 'change-reschedule':
      case 'change-cancel': {
        const request = this.changeRequests().find(r => r.id === props.refId);
        if (request) this.showChangeRequest(request);
        break;
      }
    }
  }

  /** פותח את דיאלוג הסיום עבור השיעור הראשון שממתין לסימון (מהבאנר). */
  markNextPending(): void {
    const next = this.pendingToMark()[0];
    if (next) this.completeDialog().open(next, true);
  }

  /**
   * פותח את הבקשה שהשיעור שלה הכי קרוב (מהבאנר).
   *
   * עד לשינוי הזה הדרך היחידה להגיע לבקשה הייתה ללחוץ עליה ביומן — כלומר לנחש
   * באיזה שבוע היא יושבת. המונה בלוח המחוונים קישר ליומן בשבוע הנוכחי, שבו לרוב
   * היא לא נמצאת.
   */
  openNextPendingRequest(): void {
    const next = nextPendingAnswer(this.pendingLessonRequests(), this.pendingChangeRequests());
    if (next?.kind === 'lesson') this.showLessonDetail(next.lesson);
    else if (next?.kind === 'change') this.showChangeRequest(next.request);
  }

  // ===== דיאלוג פרטי שיעור =====

  lessonDetailApprove(): void { this.fromLessonDetail(lesson => this.approveRequest(lesson)); }
  lessonDetailDecline(): void { this.fromLessonDetail(lesson => this.declineRequest(lesson)); }
  lessonDetailEditTime(): void { this.fromLessonDetail(lesson => this.openEditTimeDialog(lesson)); }
  lessonDetailComplete(): void { this.fromLessonDetail(lesson => this.completeDialog().open(lesson)); }
  lessonDetailDelete(): void { this.fromLessonDetail(lesson => this.deleteLesson(lesson)); }

  /** The clash itself, worded once and reused by the dialogs and the prompts. */
  protected conflictText(conflict: Lesson): string {
    return conflictText(conflict, this.locale);
  }

  /** סימון מהיר "התקיים" ללא תשלום/הערה — ישירות מדיאלוג הפרטים. */
  quickComplete(lesson: Lesson): void {
    this.showLessonDetailDialog.set(false);
    this.lessonsService
      .complete(lesson.id, {
        completed: true,
        paymentRequired: false,
        amount: 0,
        homework: null,
        noteContent: null,
        noteVisibleToStudent: false,
        noteVisibleToParent: false
      })
      .subscribe({
        next: () => this.lessonsChanged('השיעור סומן כהתקיים'),
        error: err => this.showError(err, 'העדכון נכשל.')
      });
  }

  /** סימון "לא הגיע" — התלמיד לא הופיע (ללא חיוב). */
  markNoShow(lesson: Lesson): void {
    this.showLessonDetailDialog.set(false);
    this.lessonsService.noShow(lesson.id).subscribe({
      next: () => this.lessonsChanged('סומן: לא הגיע'),
      error: err => this.showError(err, 'העדכון נכשל.')
    });
  }

  deleteLesson(lesson: Lesson): void {
    this.lessonsService.delete(lesson.id).subscribe({
      next: () => this.lessonsChanged('השיעור נמחק'),
      error: err => this.showError(err, 'המחיקה נכשלה.')
    });
  }

  // ===== עדכון מועד =====

  openEditTimeDialog(lesson: Lesson): void {
    this.editingLessonId.set(lesson.id);
    const start = new Date(lesson.startTime);
    const end = new Date(lesson.endTime);
    this.timeForm.reset({ date: start, startTime: start, endTime: end });
    this.showEditTimeDialog.set(true);
  }

  saveTime(): void {
    if (this.timeForm.invalid) {
      this.timeForm.markAllAsTouched();
      return;
    }
    const id = this.editingLessonId();
    if (!id) return;
    const raw = this.timeForm.getRawValue();
    const start = combineDateTime(raw.date!, raw.startTime!);
    const end = combineDateTime(raw.date!, raw.endTime!);

    const warning = preSaveWarning(this.lessons(), this.availability(), start, end, this.locale, id);
    if (warning) confirmAnyway(this.confirmationService, warning, () => this.doUpdateTime(id, start, end));
    else this.doUpdateTime(id, start, end);
  }

  private doUpdateTime(id: string, start: Date, end: Date): void {
    this.savingTime.set(true);
    this.lessonsService.update(id, { startTime: start.toISOString(), endTime: end.toISOString() }).subscribe({
      next: () => {
        this.savingTime.set(false);
        this.showEditTimeDialog.set(false);
        this.lessonsChanged('המועד עודכן');
      },
      error: err => {
        this.savingTime.set(false);
        this.showError(err, 'עדכון המועד נכשל.');
      }
    });
  }

  // ===== ביטול סדרה =====

  openCancelSeriesDialog(): void {
    const lesson = this.selectedLesson();
    if (!lesson?.seriesId) return;
    this.showLessonDetailDialog.set(false);
    this.cancellingSeriesId.set(lesson.seriesId);
    this.cancelSeriesDeleteFuture.set(false);
    this.showCancelSeriesDialog.set(true);
  }

  /** כמה שיעורים תמחק הסימון "למחוק גם שיעורים עתידיים" — ראו futureOccurrencesOfSeries. */
  protected futureOccurrencesOfSeries(): number {
    const seriesId = this.cancellingSeriesId();
    return seriesId ? futureOccurrencesOfSeries(this.lessons(), seriesId, Date.now()) : 0;
  }

  confirmCancelSeries(): void {
    const seriesId = this.cancellingSeriesId();
    if (!seriesId) return;
    this.cancellingSeries.set(true);
    this.lessonsService.cancelSeries(seriesId, this.cancelSeriesDeleteFuture()).subscribe({
      next: result => {
        this.cancellingSeries.set(false);
        this.showCancelSeriesDialog.set(false);
        // המספר מגיע מהשרת: הוא יודע מה באמת נמחק, גם אם המסך הספיק להתיישן.
        this.messageService.add({
          severity: 'success',
          summary: 'הסדרה בוטלה',
          detail: result.cancelledCount > 0
            ? `${result.cancelledCount} שיעורים עתידיים בוטלו, והמשפחה קיבלה הודעה.`
            : 'לא ייווצרו שיעורים חדשים מהסדרה. שיעורים שכבר נקבעו נשארו ביומן.'
        });
        this.loadLessons();
      },
      error: err => {
        this.cancellingSeries.set(false);
        this.showError(err, 'ביטול הסדרה נכשל.');
      }
    });
  }

  // ===== בקשות: שיעור חדש, ושינוי/ביטול לשיעור קיים =====

  changeRequestDetailApprove(): void {
    const request = this.selectedChangeRequest();
    if (!request) return;
    this.showChangeRequestDialog.set(false);
    this.approveChangeRequest(request);
  }

  changeRequestDetailReject(): void {
    const request = this.selectedChangeRequest();
    if (!request) return;
    this.showChangeRequestDialog.set(false);
    this.rejectChangeRequest(request);
  }

  approveRequest(lesson: Lesson): void {
    const conflict = findConflict(this.lessons(), new Date(lesson.startTime), new Date(lesson.endTime), lesson.id);
    this.confirmIfConflict(conflict, () =>
      this.answerRequest(lesson.id, this.lessonsService.approveRequest(lesson.id), 'הבקשה אושרה', 'האישור נכשל.', () => this.loadLessons()));
  }

  declineRequest(lesson: Lesson): void {
    this.answerRequest(lesson.id, this.lessonsService.declineRequest(lesson.id), 'הבקשה נדחתה', 'הדחייה נכשלה.', () => this.loadLessons());
  }

  approveChangeRequest(request: ChangeRequest): void {
    this.confirmIfConflict(findChangeRequestConflict(this.lessons(), request), () =>
      this.answerRequest(request.id, this.lessonsService.approveChangeRequest(request.id), 'הבקשה אושרה', 'האישור נכשל.', () => {
        this.loadLessons();
        this.loadChangeRequests();
      }));
  }

  rejectChangeRequest(request: ChangeRequest): void {
    this.answerRequest(request.id, this.lessonsService.rejectChangeRequest(request.id), 'הבקשה נדחתה', 'הדחייה נכשלה.', () => this.loadChangeRequests());
  }

  // ===== טעינה =====

  protected loadLessons(): void {
    this.loading.set(true);
    this.lessonsService.list().subscribe({
      next: lessons => {
        this.lessons.set(lessons);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  protected loadPersonalEvents(): void {
    this.calendarEventsService.list().subscribe({
      next: events => this.personalEvents.set(events),
      error: () => undefined
    });
  }

  private loadChangeRequests(): void {
    this.changeRequestsLoading.set(true);
    this.lessonsService.listChangeRequests(ChangeRequestStatus.Pending).subscribe({
      next: requests => {
        this.changeRequests.set(requests);
        this.changeRequestsLoading.set(false);
      },
      error: () => this.changeRequestsLoading.set(false)
    });
  }

  // ----- עזר -----

  private showLessonDetail(lesson: Lesson): void {
    this.selectedLesson.set(lesson);
    this.showLessonDetailDialog.set(true);
  }

  private showChangeRequest(request: ChangeRequest): void {
    this.selectedChangeRequest.set(request);
    this.showChangeRequestDialog.set(true);
  }

  private fromLessonDetail(action: (lesson: Lesson) => void): void {
    const lesson = this.selectedLesson();
    if (!lesson) return;
    this.showLessonDetailDialog.set(false);
    action(lesson);
  }

  private format(value: string | Date, pattern: string): string {
    return formatDate(value, pattern, this.locale);
  }

  /**
   * גרירה או מתיחה של פריט ביומן. אירוע אישי נשמר מיד; שיעור — רק אחרי אישור,
   * כי ההורה מקבל עליו הודעה.
   */
  private onLessonTimeDragged(
    change: CalendarEventDrop,
    describe: (lesson: Lesson) => { header: string; message: string; icon: string; success: string; failure: string }
  ): void {
    const personal = this.personalEvents().find(e => e.id === change.refId);
    if (personal) {
      this.movePersonalEvent(personal, change);
      return;
    }

    const lesson = this.lessons().find(l => l.id === change.refId);
    if (!lesson) {
      change.revert();
      return;
    }

    const text = describe(lesson);
    this.confirmationService.confirm({
      header: text.header,
      message: text.message,
      icon: text.icon,
      acceptLabel: 'עדכן',
      rejectLabel: 'ביטול',
      accept: () =>
        this.lessonsService.update(lesson.id, { startTime: change.start.toISOString(), endTime: change.end.toISOString() }).subscribe({
          next: () => this.lessonsChanged(text.success),
          error: err => {
            change.revert();
            this.showError(err, text.failure);
          }
        }),
      reject: () => change.revert()
    });
  }

  /** גרירת אירוע אישי — נשמר מיד, בלי אישור: אין לו הורה שצריך לעדכן. */
  private movePersonalEvent(item: CalendarEventItem, change: CalendarEventDrop): void {
    this.calendarEventsService
      .update(item.id, {
        title: item.title,
        startTime: change.start.toISOString(),
        endTime: change.end.toISOString(),
        isAllDay: item.isAllDay,
        notes: item.notes
      })
      .subscribe({
        next: () => this.loadPersonalEvents(),
        error: err => {
          change.revert();
          this.showError(err, 'עדכון האירוע נכשל.');
        }
      });
  }

  private confirmIfConflict(conflict: Lesson | null, proceed: () => void): void {
    if (conflict) confirmAnyway(this.confirmationService, `${this.conflictText(conflict)}. לאשר בכל זאת?`, proceed, 'אשר בכל זאת');
    else proceed();
  }

  /** תשובה על בקשה: הכפתור שלה במצב טעינה עד שהשרת עונה. */
  private answerRequest(
    id: string,
    call: ReturnType<LessonsService['approveRequest']>,
    success: string,
    failure: string,
    reload: () => void
  ): void {
    this.busyRequestId.set(id);
    call.subscribe({
      next: () => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'success', summary: success });
        reload();
      },
      error: err => {
        this.busyRequestId.set(null);
        this.showError(err, failure);
      }
    });
  }

  private lessonsChanged(summary: string): void {
    this.messageService.add({ severity: 'success', summary });
    this.loadLessons();
  }

  private showError(err: unknown, fallback: string): void {
    this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, fallback) });
  }
}
