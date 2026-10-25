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
  orgAlias: string;
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
  orgId: string;
  orgAlias: string;
  /** Tailwind CSS background class for the user's avatar chip. */
  avatarClass: string;
}

export interface RefreshTokenRequest {
  /**
   * Optional: the SPA omits it and relies on the HttpOnly refresh cookie the browser sends
   * automatically. Only non-cookie callers (e.g. the HUB SSO handoff) populate it.
   */
  refreshToken?: string;
}
