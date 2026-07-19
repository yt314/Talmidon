export interface OpenCharge {
  lessonId: string;
  studentId: string;
  studentName: string;
  lessonStartTime: string;
  amount: number;
}

export interface CreatePaymentRequest {
  parentId: string;
  lessonIds: string[];
  paidDate: string;
  method?: string | null;
  note?: string | null;
}

export interface Payment {
  id: string;
  parentId: string;
  parentName: string;
  amount: number;
  paidDate: string;
  method: string | null;
  note: string | null;
  lessonCount: number;
  confirmationSentAt: string | null;
}

export interface PaymentLesson {
  lessonId: string;
  studentId: string;
  studentName: string;
  startTime: string;
  amount: number;
}

export interface PaymentDetail {
  id: string;
  parentId: string;
  parentName: string;
  amount: number;
  paidDate: string;
  method: string | null;
  note: string | null;
  confirmationSentAt: string | null;
  lessons: PaymentLesson[];
}
