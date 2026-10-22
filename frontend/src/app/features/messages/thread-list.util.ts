import { ChatMessage, MessageAuthor, ThreadSummary } from './messages.models';

/**
 * עדכוני רשימת השיחות בצד הלקוח, אחרי פעולה שהשרת כבר אישר — כדי לא לטעון את
 * התיבה מחדש. משותף לתיבת המורה ולתיבה של התלמידה/ההורה.
 */

/** פתיחת שיחה מסמנת אותה כנקראה בשרת; כאן מיישרים את הרשימה. */
export function markRead(list: ThreadSummary[], id: string): ThreadSummary[] {
  return list.map(t => (t.id === id ? { ...t, hasUnread: false } : t));
}

/** השורה של שיחה אחרי הודעה שנשלחה בה: התצוגה המקדימה, השולח והזמן — ושיחה סגורה נפתחת מחדש. */
export function withSentMessage(list: ThreadSummary[], id: string, message: ChatMessage, sender: MessageAuthor): ThreadSummary[] {
  return list.map(t =>
    t.id === id
      ? { ...t, lastMessagePreview: message.body, lastSenderRole: sender, lastMessageAt: message.createdAt, hasUnread: false, isClosed: false }
      : t
  );
}

/** מעלה שיחה לראש הרשימה — התיבה מסודרת לפי ההודעה האחרונה. */
export function moveToTop(list: ThreadSummary[], id: string): ThreadSummary[] {
  const moved = list.find(t => t.id === id);
  return moved ? [moved, ...list.filter(t => t.id !== id)] : list;
}
