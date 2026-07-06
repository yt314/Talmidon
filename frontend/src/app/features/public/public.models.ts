export interface PublicTeacherSummary {
  id: string;
  fullName: string;
  bio: string | null;
  defaultPricePerLesson: number;
  subjects: string[];
}

export interface PublicTeacherDetail {
  id: string;
  fullName: string;
  bio: string | null;
  defaultPricePerLesson: number;
  rulesText: string | null;
  contactInfo: string | null;
  subjects: string[];
}
