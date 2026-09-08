/** מי כתב הודעה, ומי הצד השני מול המורה. תואם ל-MessageAuthor בשרת. */
export enum MessageAuthor {
  Teacher = 0,
  Parent = 1,
  Student = 2
}

export interface ChatMessage {
  id: string;
  senderRole: MessageAuthor;
  body: string;
  createdAt: string;
}

/** שורה בתיבה — בלי ההודעות עצמן. */
export interface ThreadSummary {
  id: string;
  studentId: string;
  studentName: string;
  counterpartRole: MessageAuthor;
  counterpartName: string;
  subject: string;
  lastMessagePreview: string;
  lastSenderRole: MessageAuthor;
  lastMessageAt: string;
  hasUnread: boolean;
  isClosed: boolean;
}

export interface ThreadDetail {
  id: string;
  studentId: string;
  studentName: string;
  counterpartRole: MessageAuthor;
  counterpartName: string;
  subject: string;
  relatedNoteId: string | null;
  relatedNoteContent: string | null;
  isClosed: boolean;
  createdAt: string;
  messages: ChatMessage[];
}

/** נמען אפשרי לשיחה שהמורה פותחת. */
export interface MessageRecipient {
  studentId: string;
  studentName: string;
  role: MessageAuthor;
  id: string;
  name: string;
}

export interface UnreadCount {
  count: number;
}

export const AUTHOR_LABELS: Record<MessageAuthor, string> = {
  [MessageAuthor.Teacher]: 'המורה',
  [MessageAuthor.Parent]: 'הורה',
  [MessageAuthor.Student]: 'תלמידה'
};
