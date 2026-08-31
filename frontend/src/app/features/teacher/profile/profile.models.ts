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
  bio: string | null;
  defaultPricePerLesson: number;
  defaultDurationMinutes: number;
  rulesText: string | null;
  contactInfo: string | null;
  isPublic: boolean;
  subjects: Subject[];
}

export interface UpdateTeacherProfileRequest {
  phone?: string | null;
  bio?: string | null;
  defaultPricePerLesson: number;
  defaultDurationMinutes: number;
  rulesText?: string | null;
  contactInfo?: string | null;
  isPublic: boolean;
}
