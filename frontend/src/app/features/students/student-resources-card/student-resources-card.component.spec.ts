import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { StudentResource } from '../../resources/resources.models';
import { ResourcesService } from '../../resources/resources.service';
import { StudentResourcesCardComponent } from './student-resources-card.component';

describe('StudentResourcesCardComponent', () => {
  let component: StudentResourcesCardComponent;
  let service: Record<'listForStudent' | 'create' | 'delete', ReturnType<typeof vi.fn>>;
  let confirmations: Confirmation[];

  beforeEach(() => {
    service = {
      listForStudent: vi.fn(() => of([])),
      create: vi.fn(() => of({})),
      delete: vi.fn(() => of(undefined))
    };
    confirmations = [];
    TestBed.configureTestingModule({
      imports: [StudentResourcesCardComponent],
      providers: [MessageService, ConfirmationService, { provide: ResourcesService, useValue: service }]
    });
    vi.spyOn(TestBed.inject(ConfirmationService), 'confirm').mockImplementation(c => {
      confirmations.push(c);
      return TestBed.inject(ConfirmationService);
    });
    const fixture = TestBed.createComponent(StudentResourcesCardComponent);
    fixture.componentRef.setInput('studentId', 's1');
    component = fixture.componentInstance;
    component.ngOnInit();
  });

  const form = () => component['form'];

  it('שומר קישור בלי רווחים, ותיאור ריק כ-null', () => {
    component['openAdd']();
    form().patchValue({ title: 'דף תרגול', url: '  https://example.com/page  ', description: '' });

    component['save']();

    expect(service.create).toHaveBeenCalledWith('s1', { title: 'דף תרגול', url: 'https://example.com/page', description: null });
  });

  it('קישור שאינו http/https לא נשלח', () => {
    component['openAdd']();
    form().patchValue({ title: 'x', url: 'javascript:alert(1)' });

    component['save']();

    expect(service.create).not.toHaveBeenCalled();
  });

  it('מחיקה רק אחרי אישור, ושואלת על החומר בשמו', () => {
    component['confirmDelete']({ id: 'r1', title: 'דף תרגול' } as StudentResource);
    expect(confirmations[0].message).toContain('דף תרגול');
    expect(service.delete).not.toHaveBeenCalled();

    confirmations[0].accept!();
    expect(service.delete).toHaveBeenCalledWith('s1', 'r1');
  });
});
