import { describe, expect, it } from 'vitest';
import { LessonStatus } from './lessons.models';
import { declinedPortalRequests, upcomingPortalLessons } from './portal-lessons.util';

const now = new Date('2026-03-10T08:00:00Z');

function lesson(status: LessonStatus, startTime: string) {
  return { id: `${status}-${startTime}`, status, startTime };
}

describe('upcomingPortalLessons', () => {
  it('כולל בקשה שממתינה לאישור לצד שיעור מתוזמן', () => {
    const lessons = [
      lesson(LessonStatus.Scheduled, '2026-03-12T14:00:00Z'),
      lesson(LessonStatus.Requested, '2026-03-11T14:00:00Z')
    ];
    expect(upcomingPortalLessons(lessons, now).map(l => l.status)).toEqual([
      LessonStatus.Requested,
      LessonStatus.Scheduled
    ]);
  });

  it('מדלג על שיעור שכבר עבר', () => {
    const lessons = [lesson(LessonStatus.Scheduled, '2026-03-09T14:00:00Z')];
    expect(upcomingPortalLessons(lessons, now)).toEqual([]);
  });

  it('מדלג על שיעור שבוטל, הושלם או נדחה', () => {
    const lessons = [
      lesson(LessonStatus.Cancelled, '2026-03-12T14:00:00Z'),
      lesson(LessonStatus.Completed, '2026-03-12T15:00:00Z'),
      lesson(LessonStatus.Declined, '2026-03-12T16:00:00Z')
    ];
    expect(upcomingPortalLessons(lessons, now)).toEqual([]);
  });

  it('מגביל לחמישה, הקרוב ביותר ראשון', () => {
    const lessons = Array.from({ length: 8 }, (_, i) =>
      lesson(LessonStatus.Scheduled, `2026-03-${20 - i}T14:00:00Z`)
    );
    const result = upcomingPortalLessons(lessons, now);
    expect(result).toHaveLength(5);
    expect(result[0].startTime).toBe('2026-03-13T14:00:00Z');
  });
});

describe('declinedPortalRequests', () => {
  it('מחזיר בקשה שנדחתה כשהמועד שלה עוד לפנינו', () => {
    const lessons = [lesson(LessonStatus.Declined, '2026-03-12T14:00:00Z')];
    expect(declinedPortalRequests(lessons, now)).toHaveLength(1);
  });

  it('מפסיק להציג דחייה אחרי שהמועד עבר — אין יותר מה לעשות איתה', () => {
    const lessons = [lesson(LessonStatus.Declined, '2026-03-09T14:00:00Z')];
    expect(declinedPortalRequests(lessons, now)).toEqual([]);
  });

  it('אינו מערבב בקשה שעדיין ממתינה או שיעור שבוטל', () => {
    const lessons = [
      lesson(LessonStatus.Requested, '2026-03-12T14:00:00Z'),
      lesson(LessonStatus.Cancelled, '2026-03-12T15:00:00Z')
    ];
    expect(declinedPortalRequests(lessons, now)).toEqual([]);
  });

  it('מסדר לפי מועד, הקרוב ביותר ראשון', () => {
    const lessons = [
      lesson(LessonStatus.Declined, '2026-03-14T14:00:00Z'),
      lesson(LessonStatus.Declined, '2026-03-11T14:00:00Z')
    ];
    expect(declinedPortalRequests(lessons, now).map(l => l.startTime)).toEqual([
      '2026-03-11T14:00:00Z',
      '2026-03-14T14:00:00Z'
    ]);
  });
});
