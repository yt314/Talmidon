import { Gender } from '../../core/models/gender';

export interface Parent {
  id: string;
  fullName: string;
  gender: Gender | null;
  email: string;
  phone: string | null;
  studentCount: number;
}

export interface CreateParentRequest {
  fullName: string;
  gender?: Gender | null;
  email: string;
  phone?: string | null;
}

export interface UpdateParentRequest {
  fullName: string;
  gender?: Gender | null;
  phone?: string | null;
}

export interface MyParentProfile {
  fullName: string;
  gender: Gender | null;
}
