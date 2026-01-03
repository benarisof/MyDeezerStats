export interface AuthResponse {
  token: TokenResponse;  
}

export interface TokenResponse {
  success: boolean;
  token: string;
  expiresAt: string;
  userId: string;
  message: string | null;
  errors: any[];
}

export interface SignInResponse {
  isSuccess?: boolean;
  message?: string; 
}

export interface SignUpResponse {
  success: boolean;
  message: string;
}