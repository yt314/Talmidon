export interface Subject {
  id: string;
  name: string;
}

export interface TeacherProfile {
  id: string;
  fullName: string;
  phone: string | null;
  bio: string | null;
  defaultPricePerLesson: number;
  rulesText: string | null;
  contactInfo: string | null;
  isPublic: boolean;
  subjects: Subject[];
}

export interface UpdateTeacherProfileRequest {
  phone?: string | null;
  bio?: string | null;
  defaultPricePerLesson: number;
  rulesText?: string | null;
  contactInfo?: string | null;
  isPublic: boolean;
}
