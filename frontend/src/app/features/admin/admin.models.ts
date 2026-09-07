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
