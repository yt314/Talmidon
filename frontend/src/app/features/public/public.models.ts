export interface PublicTeacherSummary {
  id: string;
  fullName: string;
  bio: string | null;
  city: string | null;
  neighborhood: string | null;
  defaultPricePerLesson: number;
  subjects: string[];
  photoVersion: number | null;
}

export interface PublicTeacherDetail {
  id: string;
  fullName: string;
  bio: string | null;
  city: string | null;
  neighborhood: string | null;
  phone: string | null;
  contactEmail: string | null;
  defaultPricePerLesson: number;
  rulesText: string | null;
  subjects: string[];
  photoVersion: number | null;
}
