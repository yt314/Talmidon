export interface AiAvailability {
  lessonPlanner: boolean;
}

export interface BuildLessonPlanRequest {
  subject: string;
  topic: string;
  durationMinutes: number;
  gradeLevel: string | null;
  notes: string | null;
}

export interface LessonPlanResponse {
  plan: string;
}
