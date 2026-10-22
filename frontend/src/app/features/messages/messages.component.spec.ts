import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MessageAuthor, ThreadDetail, ThreadSummary } from './messages.models';
import { MessagesService } from './messages.service';
import { MessagesComponent } from './messages.component';

const row = (id: string, extra: Partial<ThreadSummary> = {}): ThreadSummary => ({
  id, studentId: 's1', studentName: 'דנה', counterpartRole: MessageAuthor.Parent, counterpartName: 'אמא',
  subject: 'נושא', lastMessagePreview: '', lastSenderRole: MessageAuthor.Parent,
  lastMessageAt: '2026-03-01T10:00:00Z', hasUnread: true, isClosed: false, ...extra
});
const detail = (id: string): ThreadDetail => ({ ...row(id), messages: [] }) as unknown as ThreadDetail;

/** תיבת ההודעות של המורה. */
describe('MessagesComponent', () => {
  let service: Record<'inbox' | 'thread' | 'teacherReply' | 'setClosed' | 'recipients' | 'startThread', ReturnType<typeof vi.fn>>;

  function create(queryParams: Record<string, string> = {}): MessagesComponent {
    TestBed.configureTestingModule({
      imports: [MessagesComponent],
      providers: [
        MessageService,
        { provide: MessagesService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    });
    const component = TestBed.createComponent(MessagesComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  beforeEach(() => {
    service = {
      inbox: vi.fn(() => of([row('a'), row('b', { isClosed: true })])),
      thread: vi.fn((id: string) => of(detail(id))),
      teacherReply: vi.fn(() => of({ id: 'm', senderRole: MessageAuthor.Teacher, body: 'תשובה', createdAt: '2026-03-02T10:00:00Z' })),
      setClosed: vi.fn(() => of(undefined)),
      recipients: vi.fn(() => of([{ studentId: 's1', studentName: 'דנה', role: MessageAuthor.Parent, id: 'p1', name: 'רחל' }])),
      startThread: vi.fn(() => of(detail('new')))
    };
  });

  it('קישור מהתראה (?thread=) פותח את השיחה ומסמן אותה כנקראה', () => {
    const component = create({ thread: 'b' });
    expect(component['selected']()?.id).toBe('b');
    expect(component['threads']().find(t => t.id === 'b')!.hasUnread).toBe(false);
  });

  it('"פתוחות" מסתיר שיחות סגורות', () => {
    const component = create();
    component['showClosed'].set(false);
    expect(component['visible']().map(t => t.id)).toEqual(['a']);
  });

  it('תשובה מוסיפה את ההודעה לשיחה ומעלה את השיחה לראש התיבה', () => {
    const component = create();
    component['open'](row('b'));

    component['send']('תשובה');

    expect(service.teacherReply).toHaveBeenCalledWith('b', 'תשובה');
    expect(component['selected']()!.messages.map(m => m.body)).toEqual(['תשובה']);
    expect(component['threads']()[0]).toMatchObject({ id: 'b', lastMessagePreview: 'תשובה', isClosed: false });
  });

  it('סגירת שיחה מעדכנת גם את הרשימה', () => {
    const component = create();
    component['open'](row('a'));

    component['setClosed'](true);

    expect(service.setClosed).toHaveBeenCalledWith('a', true);
    expect(component['threads']().find(t => t.id === 'a')!.isClosed).toBe(true);
  });

  it('נמען מוצג כ"הורה · בעניין התלמידה"', () => {
    const component = create();
    component['openCompose']();
    expect(component['recipients']()[0].label).toBe('רחל · בעניין דנה');
  });

  it('שיחה חדשה נשלחת רק עם נמען, נושא ותוכן — בלי רווחים מיותרים', () => {
    const component = create();
    component['openCompose']();
    component['subject'].set('  שיעורי בית ');
    component['body'].set('  ');
    component['startThread']();
    expect(service.startThread).not.toHaveBeenCalled();

    component['recipient'].set(component['recipients']()[0]);
    component['body'].set(' שלום ');
    component['startThread']();

    expect(service.startThread).toHaveBeenCalledWith({
      studentId: 's1', counterpartRole: MessageAuthor.Parent, counterpartId: 'p1', subject: 'שיעורי בית', body: 'שלום'
    });
    expect(component['composeOpen']()).toBe(false);
  });
});
