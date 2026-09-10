export interface Subject {
  id: string;
  name: string;
}

/** חלון זמינות שבועי. dayOfWeek: ראשון=0 ... שבת=6. שעות בפורמט "HH:mm". */
export interface AvailabilityWindow {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
}

export interface TeacherProfile {
  id: string;
  fullName: string;
  phone: string | null;
  contactEmail: string | null;
  city: string | null;
  neighborhood: string | null;
  bio: string | null;
  defaultPricePerLesson: number;
  /** מחיר ל-45 דקות. null = לא הוגדר, והמחיר ייגזר יחסית מהמחיר לשעה. */
  pricePer45Minutes: number | null;
  /** מחיר ל-30 דקות. null = לא הוגדר. */
  pricePer30Minutes: number | null;
  defaultDurationMinutes: number;
  rulesText: string | null;
  isPublic: boolean;
  subjects: Subject[];
  /** חותם גרסה לתמונה, או null כשאין. הכתובת נבנית ב-teacherPhotoUrl. */
  photoVersion: number | null;
  /** מחושב בשרת (TeacherProfileRules) כדי שהממשק לא יחזיק עותק שני של הכלל. */
  isProfileComplete: boolean;
  acceptingStudents: boolean;
}

export interface UpdateTeacherProfileRequest {
  phone?: string | null;
  contactEmail?: string | null;
  city?: string | null;
  neighborhood?: string | null;
  bio?: string | null;
  defaultPricePerLesson: number;
  /** מחיר ל-45 דקות. null = לא הוגדר, והמחיר ייגזר יחסית מהמחיר לשעה. */
  pricePer45Minutes: number | null;
  /** מחיר ל-30 דקות. null = לא הוגדר. */
  pricePer30Minutes: number | null;
  defaultDurationMinutes: number;
  rulesText?: string | null;
  isPublic: boolean;
  acceptingStudents?: boolean;
}
