import { Injectable } from '@angular/core';
import { UserProfile } from '../../models/user.model';
import { StorageService } from './storage.service';
import { StorageKeys } from '../constants/storage-keys.constant';

/**
 * Exposes the current user's identity (id, email, name) to code that needs it synchronously.
 *
 * Since the switch to HttpOnly cookie auth, the JWT is no longer readable in JS, so identity is
 * sourced from the persisted user profile (written by AuthService on login) rather than by
 * decoding the access token. Token lifetime/expiry is now owned entirely by the cookie and the
 * server, so this service no longer reports expiry.
 */
@Injectable({ providedIn: 'root' })
export class TokenService {
  constructor(private readonly storage: StorageService) {}

  private get profile(): UserProfile | null {
    return this.storage.get<UserProfile>(StorageKeys.userProfile);
  }

  /** @returns The current user's id, or null when signed out. */
  get userId(): string | null {
    return this.profile?.userId ?? null;
  }

  /** @returns The current user's email, or null when signed out. */
  get email(): string | null {
    return this.profile?.email ?? null;
  }

  /** @returns The current user's display name, or null when signed out. */
  get name(): string | null {
    return this.profile?.name ?? null;
  }
}
