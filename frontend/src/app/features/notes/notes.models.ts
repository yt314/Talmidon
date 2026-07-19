export interface Note {
  id: string;
  studentId: string;
  studentName: string;
  lessonId: string | null;
  content: string;
  visibleToStudent: boolean;
  visibleToParent: boolean;
  createdAt: string;
}

export interface CreateNoteRequest {
  studentId: string;
  lessonId?: string | null;
  content: string;
  visibleToStudent: boolean;
  visibleToParent: boolean;
}

export interface UpdateNoteRequest {
  content: string;
  visibleToStudent: boolean;
  visibleToParent: boolean;
}
