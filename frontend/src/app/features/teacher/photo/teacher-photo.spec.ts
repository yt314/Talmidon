import { of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { TeacherProfileService } from '../profile/profile.service';
import { TeacherPhoto } from './teacher-photo';

function photoWith(service: Partial<Record<'uploadPhoto' | 'deletePhoto', ReturnType<typeof vi.fn>>>) {
  return new TeacherPhoto(service as unknown as TeacherProfileService);
}

describe('TeacherPhoto', () => {
  it('מציג את התמונה עם חותם הגרסה שלה, ובלי תמונה — כלום', () => {
    const photo = photoWith({});
    photo.show('t1', 64);
    expect(photo.url()).toContain('t1');
    expect(photo.url()).toContain('64');

    photo.show('t1', null);
    expect(photo.url()).toBeNull();
  });

  it('קובץ שאינו תמונה נדחה, והשדה מתאפס כדי שאותו קובץ יוכל להיבחר שוב', async () => {
    const uploadPhoto = vi.fn();
    const photo = photoWith({ uploadPhoto });
    const input = { files: [new File(['x'], 'a.pdf', { type: 'application/pdf' })], value: 'a.pdf' };

    await photo.upload({ target: input } as unknown as Event);

    expect(photo.error()).toBe('יש לבחור קובץ תמונה.');
    expect(input.value).toBe('');
    expect(uploadPhoto).not.toHaveBeenCalled();
  });

  it('הסרה מנקה את התמונה; כשל מוצג כשגיאה', () => {
    const ok = photoWith({ deletePhoto: vi.fn(() => of(undefined)) });
    ok.show('t1', 64);
    ok.remove();
    expect(ok.url()).toBeNull();
    expect(ok.busy()).toBe(false);

    const failing = photoWith({ deletePhoto: vi.fn(() => throwError(() => new Error('boom'))) });
    failing.remove();
    expect(failing.error()).toBeTruthy();
    expect(failing.busy()).toBe(false);
  });
});
