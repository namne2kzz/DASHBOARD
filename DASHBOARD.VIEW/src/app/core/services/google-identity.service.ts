import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Minimal shape of the Google Identity Services global loaded via the script tag in index.html. */
declare const google: {
  accounts: {
    id: {
      initialize(config: { client_id: string; callback: (response: { credential: string }) => void }): void;
      renderButton(parent: HTMLElement, options: GoogleButtonOptions): void;
    };
  };
};

/** Rendering options passed straight through to Google's `renderButton`. */
export interface GoogleButtonOptions {
  theme?: 'outline' | 'filled_blue' | 'filled_black';
  size?: 'large' | 'medium' | 'small';
  shape?: 'rectangular' | 'pill' | 'circle' | 'square';
  type?: 'standard' | 'icon';
  width?: number;
}

const DEFAULT_BUTTON_OPTIONS: GoogleButtonOptions = { theme: 'outline', size: 'large', width: 288, shape: 'pill' };

/** Wraps Google Identity Services (GSI) sign-in for flows that need a Google id_token, e.g. accepting an email invite or logging in. */
@Injectable({ providedIn: 'root' })
export class GoogleIdentityService {
  /**
   * Renders the "Sign in with Google" button into the given container and resolves with the
   * signed id_token once the user picks an account. Rejects if the GSI script failed to load
   * (e.g. blocked by an ad blocker or offline).
   * @param container Element the Google-rendered button is mounted into.
   * @param options Optional overrides for Google's button appearance (theme/size/shape/type/width).
   * @returns Promise resolving to the raw Google id_token (JWT) to send to the backend.
   */
  renderSignInButton(container: HTMLElement, options?: GoogleButtonOptions): Promise<string> {
    return new Promise((resolve, reject) => {
      if (typeof google === 'undefined' || !google.accounts?.id) {
        reject(new Error('Google Sign-In is unavailable right now. Please refresh and try again.'));
        return;
      }

      google.accounts.id.initialize({
        client_id: environment.googleClientId,
        callback: response => resolve(response.credential),
      });
      google.accounts.id.renderButton(container, { ...DEFAULT_BUTTON_OPTIONS, ...options });
    });
  }
}
