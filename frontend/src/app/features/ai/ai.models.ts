export interface AiAvailability {
  lessonPlanner: boolean;
  /** שם הספק הפעיל ("Gemini", "Claude", "none"). */
  provider: string;
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
