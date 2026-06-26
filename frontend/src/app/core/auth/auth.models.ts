export type Role = 'Teacher' | 'Parent' | 'Student' | 'Admin';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  phone?: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  email: string;
  roles: Role[];
}

export interface MessageResponse {
  message: string;
}
