export interface Parent {
  id: string;
  fullName: string;
  email: string;
  phone: string | null;
  studentCount: number;
}

export interface CreateParentRequest {
  fullName: string;
  email: string;
  phone?: string | null;
}

export interface UpdateParentRequest {
  fullName: string;
  phone?: string | null;
}
