import { TestBed } from '@angular/core/testing';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TeacherProfileService } from '../profile/profile.service';
import { AvailabilityEditorComponent } from './availability-editor.component';

describe('AvailabilityEditorComponent', () => {
  let component: AvailabilityEditorComponent;
  let service: Record<'getAvailability' | 'updateAvailability', ReturnType<typeof vi.fn>>;

  beforeEach(() => {
    service = {
      getAvailability: vi.fn(() => of([{ dayOfWeek: 0, startTime: '16:00', endTime: '19:00' }])),
      updateAvailability: vi.fn(() => of(undefined))
    };
    TestBed.configureTestingModule({
      imports: [AvailabilityEditorComponent],
      providers: [MessageService, { provide: TeacherProfileService, useValue: service }]
    });
    component = TestBed.createComponent(AvailabilityEditorComponent).componentInstance;
    component.ngOnInit();
  });

  it('מציג את החלונות של כל יום בנפרד', () => {
    expect(component['windowsForDay'](0)).toHaveLength(1);
    expect(component['windowsForDay'](1)).toHaveLength(0);
  });

  it('הוספה והסרה של חלון, ושמירה של הלוח כולו', () => {
    component['addWindow'](3);
    component['removeWindow'](component['windowsForDay'](0)[0]);

    component['save']();

    expect(service.updateAvailability).toHaveBeenCalledWith([{ dayOfWeek: 3, startTime: '09:00', endTime: '10:00' }]);
  });

  it('חלון שבו הסיום אינו אחרי ההתחלה — לא נשמר כלום', () => {
    component['windowsForDay'](0)[0].endTime = '16:00';

    component['save']();

    expect(service.updateAvailability).not.toHaveBeenCalled();
  });
});
