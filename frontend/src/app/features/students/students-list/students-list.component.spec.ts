import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Gender } from '../../../core/models/gender';
import { ContactRequestStatus } from '../../contact-requests/contact-requests.models';
import { ContactRequestsService } from '../../contact-requests/contact-requests.service';
import { ParentsService } from '../../parents/parents.service';
import { StudentsService } from '../students.service';
import { StudentsListComponent } from './students-list.component';

const contact = {
  id: 'c1', fullName: 'רחל לוי', phone: '0501234567', email: 'rachel@example.com',
  subject: null, message: 'שלום', status: ContactRequestStatus.New, createdAt: '2026-03-10T10:00:00Z'
};

/**
 * המסלול העדין במסך: פנייה → טופס הורה ממולא → ההורה החדש מקושר אוטומטית לטופס
 * התלמידה → אחרי השמירה הפנייה מסומנת "טופלה".
 */
describe('StudentsListComponent', () => {
  let students: Record<'list' | 'create' | 'resendInvitation', ReturnType<typeof vi.fn>>;
  let parents: Record<'list' | 'create' | 'update' | 'resendInvitation', ReturnType<typeof vi.fn>>;
  let contacts: Record<'get' | 'updateStatus', ReturnType<typeof vi.fn>>;

  function create(queryParams: Record<string, string> = {}): StudentsListComponent {
    TestBed.configureTestingModule({
      imports: [StudentsListComponent],
      providers: [
        MessageService,
        { provide: StudentsService, useValue: students },
        { provide: ParentsService, useValue: parents },
        { provide: ContactRequestsService, useValue: contacts },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    });
    const component = TestBed.createComponent(StudentsListComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  beforeEach(() => {
    students = { list: vi.fn(() => of([])), create: vi.fn(() => of({})), resendInvitation: vi.fn(() => of(undefined)) };
    parents = {
      list: vi.fn(() => of([])),
      create: vi.fn(() => of({ id: 'p-new', fullName: 'רחל לוי', email: 'rachel@example.com' })),
      update: vi.fn(() => of(undefined)),
      resendInvitation: vi.fn(() => of(undefined))
    };
    contacts = { get: vi.fn(() => of(contact)), updateStatus: vi.fn(() => of(undefined)) };
  });

  it('מפנייה: טופס ההורה נפתח עם הפרטים שכבר הוקלדו', () => {
    const component = create({ fromContact: 'c1' });

    expect(component['showParentDialog']()).toBe(true);
    expect(component['showStudentDialog']()).toBe(false);
    expect(component['parentForm'].getRawValue()).toMatchObject({ fullName: 'רחל לוי', email: 'rachel@example.com', phone: '0501234567' });
  });

  it('ההורה שנוצר מקושר לטופס התלמידה, שנפתח מיד', () => {
    const component = create({ fromContact: 'c1' });
    component['parentForm'].patchValue({ gender: Gender.Female });

    component.saveParent();

    expect(component['studentForm'].controls.parentIds.value).toEqual(['p-new']);
    expect(component['showStudentDialog']()).toBe(true);
  });

  it('אחרי שמירת התלמידה הפנייה מסומנת "טופלה"', () => {
    const component = create({ fromContact: 'c1' });
    component['studentForm'].patchValue({ fullName: 'נועה לוי', gender: Gender.Female, birthDate: new Date(2015, 4, 3) });

    component.saveStudent();

    expect(students.create).toHaveBeenCalledWith(expect.objectContaining({ fullName: 'נועה לוי', birthDate: '2015-05-03', gradeLevel: null, loginEmail: null }));
    expect(contacts.updateStatus).toHaveBeenCalledWith('c1', ContactRequestStatus.Handled);
  });

  it('פנייה שנמחקה בינתיים — נפתח טופס תלמידה רגיל', () => {
    contacts.get = vi.fn(() => throwError(() => new Error('404')));
    const component = create({ fromContact: 'gone' });

    expect(component['showStudentDialog']()).toBe(true);
    expect(component['showParentDialog']()).toBe(false);
  });

  it('בלי פנייה, הוספת תלמידה רגילה לא נוגעת בפניות', () => {
    const component = create();
    component.openStudentDialog();
    component['studentForm'].patchValue({ fullName: 'דנה', gender: Gender.Female });

    component.saveStudent();

    expect(students.create).toHaveBeenCalled();
    expect(contacts.updateStatus).not.toHaveBeenCalled();
  });
});
