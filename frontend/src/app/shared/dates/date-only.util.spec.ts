import { describe, expect, it } from 'vitest';
import { toDateOnly } from './date-only.util';

describe('toDateOnly', () => {
  it('מחזיר yyyy-MM-dd עם אפסים מובילים', () => {
    expect(toDateOnly(new Date(2026, 0, 5))).toBe('2026-01-05');
  });

  it('לפי היום המקומי — גם בחצות, שבה toISOString כבר עלול להיות ביום אחר', () => {
    expect(toDateOnly(new Date(2026, 9, 20, 0, 0))).toBe('2026-10-20');
    expect(toDateOnly(new Date(2026, 9, 20, 23, 59))).toBe('2026-10-20');
  });
});
