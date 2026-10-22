import { describe, expect, it } from 'vitest';
import { ChatMessage, MessageAuthor, ThreadSummary } from './messages.models';
import { markRead, moveToTop, withSentMessage } from './thread-list.util';

const thread = (id: string, extra: Partial<ThreadSummary> = {}): ThreadSummary => ({
  id, studentId: 's', studentName: 'דנה', counterpartRole: MessageAuthor.Parent, counterpartName: 'אמא',
  subject: 'נושא', lastMessagePreview: 'ישן', lastSenderRole: MessageAuthor.Parent,
  lastMessageAt: '2026-03-01T10:00:00Z', hasUnread: true, isClosed: false, ...extra
});

describe('thread-list.util', () => {
  it('markRead מסמן רק את השיחה שנפתחה', () => {
    const result = markRead([thread('a'), thread('b')], 'b');
    expect(result.map(t => t.hasUnread)).toEqual([true, false]);
  });

  it('withSentMessage מעדכן תצוגה מקדימה, שולח וזמן — ופותח מחדש שיחה סגורה', () => {
    const message: ChatMessage = { id: 'm', senderRole: MessageAuthor.Teacher, body: 'חדש', createdAt: '2026-03-02T10:00:00Z' };

    const [row] = withSentMessage([thread('a', { isClosed: true })], 'a', message, MessageAuthor.Teacher);

    expect(row).toMatchObject({
      lastMessagePreview: 'חדש', lastSenderRole: MessageAuthor.Teacher, lastMessageAt: message.createdAt, hasUnread: false, isClosed: false
    });
  });

  it('moveToTop מעלה את השיחה לראש הרשימה, ושיחה לא קיימת לא משנה דבר', () => {
    const list = [thread('a'), thread('b'), thread('c')];
    expect(moveToTop(list, 'c').map(t => t.id)).toEqual(['c', 'a', 'b']);
    expect(moveToTop(list, 'zzz')).toBe(list);
  });
});
