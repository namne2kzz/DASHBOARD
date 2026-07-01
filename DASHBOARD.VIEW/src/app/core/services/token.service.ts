import { Injectable } from '@angular/core';
import { JwtClaims } from '../../models/auth.model';
import { StorageService } from './storage.service';
import { StorageKeys } from '../constants/storage-keys.constant';

@Injectable({ providedIn: 'root' })
export class TokenService {
  constructor(private readonly storage: StorageService) {}

  get claims(): JwtClaims | null {
    const token = this.storage.getString(StorageKeys.accessToken);
    return token ? this.decode(token) : null;
  }

  get userId(): string | null {
    return this.claims?.sub ?? null;
  }

  get email(): string | null {
    return this.claims?.email ?? null;
  }

  get name(): string | null {
    return this.claims?.name ?? null;
  }

  get isExpired(): boolean {
    const claims = this.claims;
    if (!claims) return true;
    return Date.now() >= claims.exp * 1000;
  }

  get expiresInSeconds(): number {
    const claims = this.claims;
    if (!claims) return -1;
    return claims.exp - Math.floor(Date.now() / 1000);
  }

  /** Decodes a JWT payload without verifying signature. */
  decode(token: string): JwtClaims | null {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return null;
      const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = payload + '='.repeat((4 - (payload.length % 4)) % 4);
      return JSON.parse(atob(padded)) as JwtClaims;
    } catch {
      return null;
    }
  }
}
