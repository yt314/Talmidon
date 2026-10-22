import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ParentsService } from '../../parents/parents.service';
import { OpenCharge, OpenChargeSummary, Payment } from '../payments.models';
import { PaymentsService } from '../payments.service';
import { PaymentsListComponent } from './payments-list.component';

const charges: OpenCharge[] = [
  { lessonId: 'l1', studentId: 's1', studentName: 'דנה', lessonStartTime: '2026-03-01T10:00:00Z', amount: 120 },
  { lessonId: 'l2', studentId: 's1', studentName: 'דנה', lessonStartTime: '2026-03-08T10:00:00Z', amount: 150 }
];

const summaryRow: OpenChargeSummary = {
  parentId: 'p1', parentName: 'רחל', studentCount: 1, lessonCount: 2, total: 270, oldestLessonStartTime: '2026-03-01T10:00:00Z'
};

/** רישום תשלום — המסך שבו המורה מסמנת כסף שהתקבל. */
describe('PaymentsListComponent', () => {
  let component: PaymentsListComponent;
  let payments: Record<'openCharges' | 'openChargesSummary' | 'list' | 'create' | 'delete' | 'sendMonthlyReminders', ReturnType<typeof vi.fn>>;
  let confirmations: Confirmation[];

  beforeEach(() => {
    payments = {
      openCharges: vi.fn(() => of(charges)),
      openChargesSummary: vi.fn(() => of([summaryRow])),
      list: vi.fn(() => of([])),
      create: vi.fn(() => of({})),
      delete: vi.fn(() => of(undefined)),
      sendMonthlyReminders: vi.fn(() => of({ sentCount: 3 }))
    };
    confirmations = [];
    TestBed.configureTestingModule({
      imports: [PaymentsListComponent],
      providers: [
        MessageService,
        ConfirmationService,
        { provide: PaymentsService, useValue: payments },
        { provide: ParentsService, useValue: { list: () => of([]) } }
      ]
    });
    vi.spyOn(TestBed.inject(ConfirmationService), 'confirm').mockImplementation(c => {
      confirmations.push(c);
      return TestBed.inject(ConfirmationService);
    });
    component = TestBed.createComponent(PaymentsListComponent).componentInstance;
    component.ngOnInit();
  });

  it('מציג את סך החוב מכל ההורים', () => {
    expect(component['totalOwed']()).toBe(270);
    expect(component['totalOpenLessons']()).toBe(2);
  });

  it('פתיחת הורה מסמנת את כל החיובים שלו — המורה מורידה רק חריגים', () => {
    component.openParent(summaryRow);

    expect(payments.openCharges).toHaveBeenCalledWith('p1');
    expect(component['selectedTotal']()).toBe(270);
  });

  it('הורדת סימון משיעור מורידה אותו מהסכום, ו"הכול" מחזיר', () => {
    component.openParent(summaryRow);

    component.toggleLesson('l1');
    expect(component['selectedTotal']()).toBe(150);

    component.toggleAll();
    expect(component['selectedTotal']()).toBe(270);
    component.toggleAll();
    expect(component['selectedTotal']()).toBe(0);
  });

  it('רישום התשלום שולח את השיעורים המסומנים, התאריך כ-yyyy-MM-dd ושדות ריקים כ-null', () => {
    component.openParent(summaryRow);
    component.toggleLesson('l2');
    component['paymentForm'].patchValue({ paidDate: new Date(2026, 2, 15), method: 'מזומן' });

    component.markAsPaid();

    expect(payments.create).toHaveBeenCalledWith({
      parentId: 'p1', lessonIds: ['l1'], paidDate: '2026-03-15', method: 'מזומן', note: null
    });
  });

  it('בלי שיעור מסומן — לא נשלח, ומוצגת שגיאה', () => {
    component.openParent(summaryRow);
    component.toggleAll();

    component.markAsPaid();

    expect(payments.create).not.toHaveBeenCalled();
    expect(component['noSelectionError']()).toBe(true);
  });

  it('ביטול תשלום רק אחרי אישור', () => {
    component.confirmDeletePayment({ id: 'pay1' } as Payment);
    expect(payments.delete).not.toHaveBeenCalled();

    confirmations[0].accept!();
    expect(payments.delete).toHaveBeenCalledWith('pay1');
  });

  it('שורת "ללא הורה משויך" אינה נפתחת — אין למי לרשום תשלום', () => {
    component.openParent({ ...summaryRow, parentId: null });
    expect(payments.openCharges).not.toHaveBeenCalled();
  });
});
