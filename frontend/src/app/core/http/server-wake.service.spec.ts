import { ServerWakeService } from './server-wake.service';

/**
 * כמה בקשות רצות במקביל, ולכן ההודעה אינה מתג פשוט: היא צריכה להיכבות רק כשאין
 * יותר אף בקשה איטית. דגל בוליאני פשוט היה נכבה עם הראשונה שמסתיימת ומשאיר את
 * המבקר מול ספינר שקט בדיוק כשהוא עדיין ממתין.
 */
describe('ServerWakeService', () => {
  it('מתחיל כבוי', () => {
    expect(new ServerWakeService().waking()).toBe(false);
  });

  it('נדלק כשבקשה עוברת את הסף', () => {
    const service = new ServerWakeService();
    service.markSlow(1);
    expect(service.waking()).toBe(true);
  });

  it('נשאר דלוק כל עוד בקשה איטית אחת עדיין רצה', () => {
    const service = new ServerWakeService();
    service.markSlow(1);
    service.markSlow(2);

    service.finished(1);

    expect(service.waking()).toBe(true);
  });

  it('נכבה כשהאחרונה מסתיימת', () => {
    const service = new ServerWakeService();
    service.markSlow(1);
    service.markSlow(2);

    service.finished(1);
    service.finished(2);

    expect(service.waking()).toBe(false);
  });

  /** בקשה מהירה מסתיימת בלי שסומנה כאיטית — אסור שהיא תכבה הודעה של בקשה אחרת. */
  it('בקשה מהירה שמסתיימת אינה מכבה הודעה של אחרת', () => {
    const service = new ServerWakeService();
    service.markSlow(1);

    service.finished(99);

    expect(service.waking()).toBe(true);
  });
});
