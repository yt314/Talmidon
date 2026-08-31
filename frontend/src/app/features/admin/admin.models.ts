export interface AdminTeacher {
  id: string;
  fullName: string;
  email: string;
  createdAt: string;
  isPublic: boolean;
  studentCount: number;
  isLockedOut: boolean;
}
