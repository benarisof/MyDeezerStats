export interface TokenResponse {
  success: boolean;
  token: string;
  expiresAt?: string;
  userId: string;
  message: string | null;
}

export interface AuthResponse {
  token: TokenResponse; // Ton API renvoie un objet 'token' contenant les infos
}

export interface SignUpResponse {
  success: boolean;
  message: string;
}