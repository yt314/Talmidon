import { LessonStatus } from '../lessons/lessons.models';

export interface StudentLesson {
  id: string;
  startTime: string;
  endTime: string;
  status: LessonStatus;
  homework: string | null;
}

export interface StudentNote {
  id: string;
  content: string;
  createdAt: string;
}
