import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TeacherProfile } from '../profile/profile.models';
import { TeacherProfileService } from '../profile/profile.service';
import { ProfileSetupService } from './profile-setup.service';
import { ProfileSetupComponent } from './profile-setup.component';

const existing = {
  id: 't1', fullName: 'מורה', phone: null, contactEmail: null, city: null, neighborhood: null, bio: null,
  defaultPricePerLesson: 0, pricePer45Minutes: 110, pricePer30Minutes: null, defaultDurationMinutes: 45,
  rulesText: 'ביטול עד 24 שעות', isPublic: false, subjects: [], photoVersion: null,
  isProfileComplete: false, acceptingStudents: true
} as TeacherProfile;

/** מסך ההקמה הראשון של מורה: תחומים, מחיר ודרך ליצור קשר. */
describe('ProfileSetupComponent', () => {
  let component: ProfileSetupComponent;
  let service: Record<'getMyProfile' | 'subjectSuggestions' | 'setSubjects' | 'updateMyProfile', ReturnType<typeof vi.fn>>;
  let setup: Record<'markComplete' | 'skip', ReturnType<typeof vi.fn>>;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    service = {
      getMyProfile: vi.fn(() => of(existing)),
      subjectSuggestions: vi.fn(() => of([])),
      setSubjects: vi.fn(() => of([])),
      updateMyProfile: vi.fn(() => of(undefined))
    };
    setup = { markComplete: vi.fn(), skip: vi.fn() };
    navigate = vi.fn();
    TestBed.configureTestingModule({
      imports: [ProfileSetupComponent],
      providers: [
        MessageService,
        { provide: TeacherProfileService, useValue: service },
        { provide: ProfileSetupService, useValue: setup },
        { provide: Router, useValue: { navigate } }
      ]
    });
    component = TestBed.createComponent(ProfileSetupComponent).componentInstance;
    component.ngOnInit();
  });

  const fill = () => {
    component['subjectNames'].set(['מתמטיקה']);
    component['form'].patchValue({ defaultPricePerLesson: 150, phone: '0501234567' });
  };

  it('בלי תחום — לא נשמר כלום', () => {
    component['form'].patchValue({ defaultPricePerLesson: 150, phone: '050' });
    component.save();
    expect(service.setSubjects).not.toHaveBeenCalled();
  });

  it('בלי טלפון ובלי מייל — הטופס לא תקין; אחד מהם מספיק', () => {
    component['form'].patchValue({ defaultPricePerLesson: 150 });
    expect(component['form'].hasError('noContact')).toBe(true);

    component['form'].patchValue({ contactEmail: 'me@example.com' });
    expect(component['form'].hasError('noContact')).toBe(false);
  });

  it('שומר קודם את התחומים, ורק אחר כך את הפרופיל — בלי למחוק שדות שהמסך אינו עורך', () => {
    fill();

    component.save();

    expect(service.setSubjects).toHaveBeenCalledWith(['מתמטיקה']);
    expect(service.updateMyProfile).toHaveBeenCalledWith(expect.objectContaining({
      defaultPricePerLesson: 150, phone: '0501234567', isPublic: true,
      pricePer45Minutes: 110, defaultDurationMinutes: 45, rulesText: 'ביטול עד 24 שעות'
    }));
    expect(setup.markComplete).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/app/dashboard']);
  });

  it('אם שמירת התחומים נכשלה — הפרופיל לא נשמר ולא מוצג "מוכן"', () => {
    service.setSubjects = vi.fn(() => throwError(() => new Error('boom')));
    fill();

    component.save();

    expect(service.updateMyProfile).not.toHaveBeenCalled();
    expect(setup.markComplete).not.toHaveBeenCalled();
  });

  it('"אמלא אחר כך" ממשיך ללוח הבקרה', () => {
    component.skip();
    expect(setup.skip).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/app/dashboard']);
  });
});
