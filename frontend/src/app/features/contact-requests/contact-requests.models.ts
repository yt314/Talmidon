export enum ContactRequestStatus {
  New = 0,
  Handled = 1,
  Closed = 2
}

export interface ContactRequest {
  id: string;
  fullName: string;
  phone: string;
  email: string | null;
  subject: string | null;
  message: string;
  status: ContactRequestStatus;
  createdAt: string;
}

export interface CreateContactRequest {
  fullName: string;
  phone: string;
  email: string | null;
  subject: string | null;
  message: string;
}

export const CONTACT_STATUS_LABELS: Record<ContactRequestStatus, string> = {
  [ContactRequestStatus.New]: 'חדשה',
  [ContactRequestStatus.Handled]: 'בטיפול',
  [ContactRequestStatus.Closed]: 'נסגרה'
};

export const CONTACT_STATUS_SEVERITY: Record<ContactRequestStatus, 'warn' | 'info' | 'secondary'> = {
  [ContactRequestStatus.New]: 'warn',
  [ContactRequestStatus.Handled]: 'info',
  [ContactRequestStatus.Closed]: 'secondary'
};
