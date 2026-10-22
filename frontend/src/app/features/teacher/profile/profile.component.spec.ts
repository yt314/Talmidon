import { TestBed } from '@angular/core/testing';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ProfileSetupService } from '../profile-setup/profile-setup.service';
import { TeacherProfile } from './profile.models';
import { TeacherProfileService } from './profile.service';
import { TeacherProfileSettingsComponent } from './profile.component';

const profile = {
  id: 't1', fullName: 'מורה', phone: '050', contactEmail: null, city: 'ירושלים', neighborhood: null, bio: null,
  defaultPricePerLesson: 150, pricePer45Minutes: null, pricePer30Minutes: null, defaultDurationMinutes: 60,
  rulesText: null, isPublic: true, subjects: [{ id: 's1', name: 'מתמטיקה' }], photoVersion: null,
  isProfileComplete: true, acceptingStudents: true
} as TeacherProfile;

/** הגדרות הפרופיל של המורה — מה שמופיע בספרייה הציבורית. */
describe('TeacherProfileSettingsComponent', () => {
  let component: TeacherProfileSettingsComponent;
  let service: Record<'getMyProfile' | 'updateMyProfile' | 'setSubjects' | 'subjectSuggestions' | 'getAvailability', ReturnType<typeof vi.fn>>;
  let setup: { setComplete: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    service = {
      getMyProfile: vi.fn(() => of(profile)),
      updateMyProfile: vi.fn(() => of(undefined)),
      setSubjects: vi.fn(() => of([])),
      subjectSuggestions: vi.fn(() => of([])),
      getAvailability: vi.fn(() => of([]))
    };
    setup = { setComplete: vi.fn() };
    TestBed.configureTestingModule({
      imports: [TeacherProfileSettingsComponent],
      providers: [
        MessageService,
        { provide: TeacherProfileService, useValue: service },
        { provide: ProfileSetupService, useValue: setup }
      ]
    });
    component = TestBed.createComponent(TeacherProfileSettingsComponent).componentInstance;
    component.ngOnInit();
  });

  it('ממלא את הטופס מהפרופיל, ומעדכן את מצב השלמות המשותף', () => {
    expect(component['form'].getRawValue()).toMatchObject({ phone: '050', city: 'ירושלים', defaultPricePerLesson: 150, contactEmail: '' });
    expect(setup.setComplete).toHaveBeenCalledWith(true);
  });

  it('שמירה שולחת שדות ריקים כ-null, ובודקת מחדש את שלמות הפרופיל', () => {
    component['form'].patchValue({ city: '', bio: 'מורה למתמטיקה' });

    component.save();

    expect(service.updateMyProfile).toHaveBeenCalledWith(expect.objectContaining({ city: null, bio: 'מורה למתמטיקה', contactEmail: null }));
    expect(service.getMyProfile).toHaveBeenCalledTimes(2);
  });

  it('מחיר שלילי — לא נשמר', () => {
    component['form'].patchValue({ defaultPricePerLesson: -5 });
    component.save();
    expect(service.updateMyProfile).not.toHaveBeenCalled();
  });

  it('תחומים נשמרים מיד, בלי רווחים ובלי ריקים', () => {
    component.onSubjectsChange([' אנגלית ', '', 'כימיה']);
    expect(service.setSubjects).toHaveBeenCalledWith(['אנגלית', 'כימיה']);
  });

  it('תחום ארוך מ-100 תווים — לא נשמר, ומוצגת שגיאה', () => {
    component.onSubjectsChange(['א'.repeat(101)]);
    expect(service.setSubjects).not.toHaveBeenCalled();
    expect(component['subjectsError']()).toContain('100');
  });

  it('קובץ שאינו תמונה נדחה לפני ההעלאה', async () => {
    const input = { files: [new File(['x'], 'doc.pdf', { type: 'application/pdf' })], value: 'doc.pdf' };
    await component['photo'].upload({ target: input } as unknown as Event);
    expect(component['photo'].error()).toBe('יש לבחור קובץ תמונה.');
    expect(input.value).toBe('');
  });
});
