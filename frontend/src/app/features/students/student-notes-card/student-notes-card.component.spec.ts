import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Note } from '../../notes/notes.models';
import { NotesService } from '../../notes/notes.service';
import { StudentNotesCardComponent } from './student-notes-card.component';

const note = { id: 'n1', content: 'התקדמה יפה', visibleToStudent: true, visibleToParent: true, createdAt: '2026-03-10T10:00:00Z' } as Note;

describe('StudentNotesCardComponent', () => {
  let component: StudentNotesCardComponent;
  let service: Record<'listForStudent' | 'create' | 'update' | 'delete', ReturnType<typeof vi.fn>>;
  let confirmations: Confirmation[];

  beforeEach(() => {
    service = {
      listForStudent: vi.fn(() => of([note])),
      create: vi.fn(() => of(note)),
      update: vi.fn(() => of(undefined)),
      delete: vi.fn(() => of(undefined))
    };
    confirmations = [];
    TestBed.configureTestingModule({
      imports: [StudentNotesCardComponent],
      providers: [MessageService, ConfirmationService, { provide: NotesService, useValue: service }]
    });
    vi.spyOn(TestBed.inject(ConfirmationService), 'confirm').mockImplementation(c => {
      confirmations.push(c);
      return TestBed.inject(ConfirmationService);
    });
    const fixture = TestBed.createComponent(StudentNotesCardComponent);
    fixture.componentRef.setInput('studentId', 's1');
    component = fixture.componentInstance;
    component.ngOnInit();
  });

  const form = () => component['form'];

  it('טוען את ההערות של התלמיד/ה', () => {
    expect(service.listForStudent).toHaveBeenCalledWith('s1');
    expect(component['notes']()).toEqual([note]);
  });

  it('הערה חדשה נוצרת עבור התלמיד/ה', () => {
    component['openAdd']();
    form().patchValue({ content: 'שיעורי בית בעמ׳ 4' });

    component['save']();

    expect(service.create).toHaveBeenCalledWith({ studentId: 's1', content: 'שיעורי בית בעמ׳ 4', visibleToStudent: false, visibleToParent: false });
    expect(service.update).not.toHaveBeenCalled();
  });

  it('עריכה שומרת את ההערה הקיימת', () => {
    component['openEdit'](note);
    form().patchValue({ content: 'עודכן' });

    component['save']();

    expect(service.update).toHaveBeenCalledWith('n1', { content: 'עודכן', visibleToStudent: true, visibleToParent: true });
  });

  it('הערה ריקה לא נשלחת', () => {
    component['openAdd']();
    component['save']();
    expect(service.create).not.toHaveBeenCalled();
  });

  it('"גלוי לתלמיד" מסמן גם "גלוי להורה" — כמו שהשרת שומר', () => {
    component['openAdd']();
    component['onVisibleToStudentChange'](true);
    expect(form().controls.visibleToParent.value).toBe(true);
  });

  it('מחיקה רק אחרי אישור', () => {
    component['confirmDelete'](note);
    expect(service.delete).not.toHaveBeenCalled();

    confirmations[0].accept!();
    expect(service.delete).toHaveBeenCalledWith('n1');
  });
});
