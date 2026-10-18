export interface AdminTeacher {
  id: string;
  fullName: string;
  email: string;
  createdAt: string;
  isPublic: boolean;
  studentCount: number;
  isLockedOut: boolean;
}

/** שורה ברשימת ההצעות לתחומי לימוד, בעיני המנהל. */
export interface AdminSubjectSuggestion {
  name: string;
  isHidden: boolean;
  /** מהקטלוג הקבוע שבקוד — לא ניתן להסתרה. */
  isBuiltIn: boolean;
  /** מורה כלשהי כבר בחרה בתחום. */
  isInUse: boolean;
}

/** הודעה שנשלחה מהאתר בתקופת ההרצה. */
export interface AdminSiteFeedback {
  id: string;
  message: string;
  contactInfo: string | null;
  pageUrl: string | null;
  isHandled: boolean;
  createdAt: string;
}

/** שורה ביומן התקלות. בלי עקבת המחסנית — היא נטענת רק כשפותחים תקלה אחת. */
export interface AdminError {
  id: string;
  reference: string;
  occurredAt: string;
  /** `request` (בקשה מהדפדפן) או `job` (משימת רקע). */
  source: string;
  kind: string;
  message: string;
  path: string | null;
  method: string | null;
  userEmail: string | null;
  isHandled: boolean;
}

/** תקלה אחת, עם עקבת המחסנית המלאה. */
export interface AdminErrorDetail extends AdminError {
  detail: string | null;
  tenantId: string | null;
}
