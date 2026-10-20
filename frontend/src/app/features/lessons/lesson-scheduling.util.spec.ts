import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import {
  amountExplanation,
  amountRequiredWhenChargingValidator,
  combineDateTime,
  defaultEventStart,
  eventToFormValue,
  findChangeRequestConflict,
  findConflict,
  formValueToEventRequest,
  futureOccurrencesOfSeries,
  isOutsideAvailability,
  lessonsAwaitingMark,
  nextPendingAnswer,
  preSaveWarning,
  toMinutes
} from './lesson-scheduling.util';
import { ChangeRequest, ChangeRequestStatus, ChangeRequestType, Lesson, LessonStatus } from './lessons.models';

/** תאריך בשעון המקומי — הבדיקות לא תלויות באזור הזמן של המכונה. */
const at = (day: number, hour: number, minute = 0) => new Date(2026, 2, day, hour, minute);

function lesson(id: string, start: Date, end: Date, status = LessonStatus.Scheduled, seriesId: string | null = null): Lesson {
  return {
    id, studentId: 's1', studentName: 'דנה', startTime: start.toISOString(), endTime: end.toISOString(),
    status, homework: null, paymentRequired: false, amount: 0, isPaid: false, completedAt: null, seriesId
  } as Lesson;
}

function changeRequest(id: string, lessonStart: Date, type = ChangeRequestType.Cancel, proposed?: [Date, Date]): ChangeRequest {
  return {
    id, lessonId: `lesson-of-${id}`, studentId: 's1', studentName: 'דנה', parentName: 'אמא', type,
    lessonStartTime: lessonStart.toISOString(), lessonEndTime: lessonStart.toISOString(),
    proposedStartTime: proposed?.[0].toISOString() ?? null, proposedEndTime: proposed?.[1].toISOString() ?? null,
    reason: null, status: ChangeRequestStatus.Pending, createdAt: lessonStart.toISOString()
  };
}

describe('combineDateTime / toMinutes', () => {
  it('לוקח את היום מהתאריך ואת השעה מהשעה', () => {
    expect(combineDateTime(at(10, 0), at(1, 14, 30))).toEqual(at(10, 14, 30));
  });

  it('ממיר HH:mm לדקות, גם בלי דקות', () => {
    expect(toMinutes('09:30')).toBe(570);
    expect(toMinutes('10')).toBe(600);
  });
});

describe('findConflict', () => {
  const existing = lesson('a', at(10, 14), at(10, 15));

  it('מוצא שיעור חופף', () => {
    expect(findConflict([existing], at(10, 14, 30), at(10, 15, 30))?.id).toBe('a');
  });

  it('שיעור שמתחיל בדיוק כשהקודם נגמר אינו התנגשות', () => {
    expect(findConflict([existing], at(10, 15), at(10, 16))).toBeNull();
  });

  it('מתעלם מהשיעור עצמו בעדכון מועד', () => {
    expect(findConflict([existing], at(10, 14), at(10, 15), 'a')).toBeNull();
  });

  it('שיעור שבוטל, נדחה או שהתלמיד לא הגיע אליו אינו תופס את השעה', () => {
    for (const status of [LessonStatus.Cancelled, LessonStatus.Declined, LessonStatus.NoShow]) {
      expect(findConflict([lesson('x', at(10, 14), at(10, 15), status)], at(10, 14), at(10, 15))).toBeNull();
    }
  });

  it('בקשה ממתינה כן תופסת את השעה', () => {
    const requested = lesson('r', at(10, 14), at(10, 15), LessonStatus.Requested);
    expect(findConflict([requested], at(10, 14), at(10, 15))?.id).toBe('r');
  });
});

describe('findChangeRequestConflict', () => {
  const existing = lesson('a', at(10, 14), at(10, 15));

  it('בודק את המועד המוצע בבקשת שינוי', () => {
    const request = changeRequest('c', at(9, 10), ChangeRequestType.Reschedule, [at(10, 14), at(10, 15)]);
    expect(findChangeRequestConflict([existing], request)?.id).toBe('a');
  });

  it('בקשת ביטול אינה מתנגשת בדבר', () => {
    expect(findChangeRequestConflict([existing], changeRequest('c', at(10, 14)))).toBeNull();
  });
});

describe('preSaveWarning', () => {
  const existing = lesson('a', at(10, 14), at(10, 15));
  const windows = [{ dayOfWeek: at(10, 0).getDay(), startTime: '09:00', endTime: '18:00' }];

  it('התנגשות קודמת לשעות הזמינות, ומציינת עם מי ומתי', () => {
    expect(preSaveWarning([existing], windows, at(10, 14), at(10, 15), 'en-US'))
      .toBe('כבר קיים שיעור עם דנה בשעה 14:00–15:00. לקבוע בכל זאת?');
  });

  it('מחוץ לשעות הזמינות', () => {
    expect(preSaveWarning([existing], windows, at(10, 19), at(10, 20), 'en-US')).toContain('מחוץ לשעות הזמינות');
  });

  it('null כשהכל תקין', () => {
    expect(preSaveWarning([existing], windows, at(10, 10), at(10, 11), 'en-US')).toBeNull();
  });
});

describe('isOutsideAvailability', () => {
  const tuesday = at(10, 0).getDay();
  const windows = [{ dayOfWeek: tuesday, startTime: '09:00', endTime: '13:00' }];

  it('בלי שעות זמינות אין הגבלה', () => {
    expect(isOutsideAvailability([], at(10, 23), at(10, 23, 30))).toBe(false);
  });

  it('שיעור בתוך החלון תקין, כולל בדיוק בגבולות', () => {
    expect(isOutsideAvailability(windows, at(10, 9), at(10, 13))).toBe(false);
  });

  it('שיעור שגולש מהחלון, או ביום אחר, מחוץ לשעות', () => {
    expect(isOutsideAvailability(windows, at(10, 12, 30), at(10, 13, 30))).toBe(true);
    expect(isOutsideAvailability(windows, at(11, 10), at(11, 11))).toBe(true);
  });
});

describe('lessonsAwaitingMark', () => {
  const now = at(10, 12).getTime();

  it('רק מתוזמנים שהסתיימו, מהמוקדם', () => {
    const lessons = [
      lesson('later', at(9, 10), at(9, 11)),
      lesson('earlier', at(8, 10), at(8, 11)),
      lesson('future', at(11, 10), at(11, 11)),
      lesson('done', at(7, 10), at(7, 11), LessonStatus.Completed),
      lesson('running', at(10, 11), at(10, 13))
    ];
    expect(lessonsAwaitingMark(lessons, now).map(l => l.id)).toEqual(['earlier', 'later']);
  });
});

describe('futureOccurrencesOfSeries', () => {
  it('סופר רק שיעורים עתידיים ומתוזמנים של אותה סדרה', () => {
    const now = at(10, 12).getTime();
    const lessons = [
      lesson('past', at(3, 10), at(3, 11), LessonStatus.Scheduled, 'S'),
      lesson('f1', at(17, 10), at(17, 11), LessonStatus.Scheduled, 'S'),
      lesson('f2', at(24, 10), at(24, 11), LessonStatus.Scheduled, 'S'),
      lesson('cancelled', at(31, 10), at(31, 11), LessonStatus.Cancelled, 'S'),
      lesson('other', at(17, 10), at(17, 11), LessonStatus.Scheduled, 'T')
    ];
    expect(futureOccurrencesOfSeries(lessons, 'S', now)).toBe(2);
  });
});

describe('nextPendingAnswer', () => {
  it('בוחר את הבקשה שהשיעור שלה הכי קרוב, משני הסוגים', () => {
    const lessons = [lesson('l-late', at(20, 10), at(20, 11), LessonStatus.Requested)];
    const changes = [changeRequest('c-soon', at(12, 10)), changeRequest('c-later', at(15, 10))];
    expect(nextPendingAnswer(lessons, changes)).toEqual({ kind: 'change', request: changes[0] });
  });

  it('בתיקו — בקשה לשיעור חדש קודמת', () => {
    const lessons = [lesson('l', at(12, 10), at(12, 11), LessonStatus.Requested)];
    const changes = [changeRequest('c', at(12, 10))];
    expect(nextPendingAnswer(lessons, changes)).toEqual({ kind: 'lesson', lesson: lessons[0] });
  });

  it('null כשאין בקשות', () => {
    expect(nextPendingAnswer([], [])).toBeNull();
  });
});

describe('amountExplanation', () => {
  it('מסביר מאיפה הגיע הסכום, כולל המשך', () => {
    expect(amountExplanation({ amount: 150, durationMinutes: 45, source: 'exact' })).toBe('לפי מחירון השיעורים שלך ל-45 דק׳.');
    expect(amountExplanation({ amount: 0, durationMinutes: 60, source: 'unknown' })).toContain('ידנית');
  });
});

describe('defaultEventStart', () => {
  it('השעה העגולה הבאה', () => {
    expect(defaultEventStart(at(10, 14, 20))).toEqual(at(10, 15));
  });

  it('בערב — עוברת ל-09:00 למחרת, כדי שהאירוע ייראה ביומן', () => {
    expect(defaultEventStart(at(10, 21, 10))).toEqual(at(11, 9));
  });

  it('לפנות בוקר — 09:00 באותו יום', () => {
    expect(defaultEventStart(at(10, 3, 10))).toEqual(at(10, 9));
  });
});

describe('אירוע אישי: טופס ↔ שרת', () => {
  it('יום שלם נשמר עם סוף בלעדי (חצות של היום שאחרי)', () => {
    const request = formValueToEventRequest({
      title: '  חופשה ', date: at(10, 0), endDate: at(12, 0), startTime: null, endTime: null, isAllDay: true, notes: ' '
    });
    expect(request).toEqual({
      title: 'חופשה', startTime: at(10, 0).toISOString(), endTime: at(13, 0).toISOString(), isAllDay: true, notes: null
    });
  });

  it('תאריך סיום לפני תאריך ההתחלה מתעלם ממנו — יום אחד', () => {
    const request = formValueToEventRequest({
      title: 'x', date: at(10, 0), endDate: at(8, 0), startTime: null, endTime: null, isAllDay: true, notes: ''
    });
    expect(request?.endTime).toBe(at(11, 0).toISOString());
  });

  it('אירוע עם שעות בלי שעת סיום אינו נשלח', () => {
    expect(formValueToEventRequest({
      title: 'x', date: at(10, 0), endDate: null, startTime: at(10, 9), endTime: null, isAllDay: false, notes: ''
    })).toBeNull();
  });

  it('יום שלם אחד חוזר לטופס כיום אחד, ולא כנמשך יומיים', () => {
    const value = eventToFormValue({
      id: 'e', title: 'חג', startTime: at(10, 0).toISOString(), endTime: at(11, 0).toISOString(), isAllDay: true, notes: null
    });
    expect(value.endDate).toBeNull();
    expect(value.startTime).toBeNull();
  });

  it('טווח של כמה ימים חוזר עם היום האחרון כולל', () => {
    const value = eventToFormValue({
      id: 'e', title: 'חופשה', startTime: at(10, 0).toISOString(), endTime: at(13, 0).toISOString(), isAllDay: true, notes: null
    });
    expect(value.endDate).toEqual(at(12, 0));
  });
});

describe('amountRequiredWhenChargingValidator', () => {
  const form = (completed: boolean, paymentRequired: boolean, amount: number) =>
    new FormGroup(
      { completed: new FormControl(completed), paymentRequired: new FormControl(paymentRequired), amount: new FormControl(amount) },
      { validators: amountRequiredWhenChargingValidator }
    );

  it('שיעור שהתקיים בתשלום דורש סכום', () => {
    expect(form(true, true, 0).controls.amount.hasError('required')).toBe(true);
  });

  it('בלי תשלום, או כשהשיעור בוטל — אין דרישה', () => {
    expect(form(true, false, 0).controls.amount.valid).toBe(true);
    expect(form(false, true, 0).controls.amount.valid).toBe(true);
  });

  it('השגיאה יורדת כשמזינים סכום', () => {
    const f = form(true, true, 0);
    f.controls.amount.setValue(120);
    expect(f.controls.amount.valid).toBe(true);
  });
});
