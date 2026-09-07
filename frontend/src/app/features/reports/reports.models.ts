export interface StudentIncome {
  studentId: string;
  studentName: string;
  lessons: number;
  charged: number;
  paid: number;
}

export interface IncomeReport {
  year: number;
  month: number;
  completedLessons: number;
  totalCharged: number;
  totalPaid: number;
  totalOutstanding: number;
  byStudent: StudentIncome[];
}

export interface StudentAttendance {
  studentId: string;
  studentName: string;
  completed: number;
  cancelled: number;
  noShow: number;
  hours: number;
  /** אחוז הביטולים ואי-ההגעות מתוך השיעורים שהיו אמורים להתקיים. */
  missedPercent: number;
}

export interface AttendanceReport {
  year: number;
  month: number;
  completed: number;
  cancelled: number;
  noShow: number;
  hours: number;
  byStudent: StudentAttendance[];
}
