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
