import { Gender } from '../../core/models/gender';

export interface StudentListItem {
  id: string;
  fullName: string;
  gradeLevel: string | null;
  isActive: boolean;
  hasLogin: boolean;
  parentCount: number;
}

export interface ParentSummary {
  id: string;
  fullName: string;
  email: string;
  phone: string | null;
}

export interface StudentDetail {
  id: string;
  fullName: string;
  gender: Gender | null;
  gradeLevel: string | null;
  birthDate: string | null;
  generalInfo: string | null;
  isActive: boolean;
  hasLogin: boolean;
  parents: ParentSummary[];
}

export interface CreateStudentRequest {
  fullName: string;
  gender?: Gender | null;
  gradeLevel?: string | null;
  birthDate?: string | null;
  generalInfo?: string | null;
  loginEmail?: string | null;
  parentIds?: string[] | null;
}

export interface UpdateStudentRequest {
  fullName: string;
  gender?: Gender | null;
  gradeLevel?: string | null;
  birthDate?: string | null;
  generalInfo?: string | null;
  isActive: boolean;
}

export interface MyStudentProfile {
  fullName: string;
  gender: Gender | null;
}
