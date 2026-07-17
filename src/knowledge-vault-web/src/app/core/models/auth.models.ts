export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  userNameOrEmail: string;
  password: string;
}

export interface UserProfile {
  id: string;
  userName: string;
  email: string;
  nickname?: string | null;
  createdAt: string;
  lastLoginAt?: string | null;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
}

export interface AuthState {
  token: string | null;
  expiresAt: string | null;
  user: UserProfile | null;
}
