export interface JwtClaims {
  sub: string;
  email: string;
  name: string;
  jti: string;
  exp: number;
  iat: number;
  iss: string;
  aud: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  jwtId: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  userId: string;
  name: string;
  email: string;
  isGlobalAdmin: boolean;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}
