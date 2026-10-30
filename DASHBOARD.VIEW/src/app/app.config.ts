import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideHttpClient, withInterceptors, withNoXsrfProtection } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideToastr } from 'ngx-toastr';
import { provideAnimations } from '@angular/platform-browser/animations';

import { authInterceptor } from './interceptors/auth.interceptor';
import { correlationIdInterceptor } from './interceptors/correlation-id.interceptor';
import { errorInterceptor } from './interceptors/error.interceptor';
import { ThemeService } from './core/services/theme.service';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    // Instantiate ThemeService at bootstrap so the persisted theme is read
    // from localStorage and applied app-wide, independent of which page loads.
    provideAppInitializer(() => { inject(ThemeService); }),
    provideRouter(routes),
    provideHttpClient(
      // Angular's built-in XSRF support does not attach the header to absolute cross-origin URLs
      // (the dev setup: SPA :4200 → API :5152), so authInterceptor attaches the double-submit CSRF
      // header itself for both dev and prod. Disable the built-in handling to avoid a duplicate.
      withNoXsrfProtection(),
      withInterceptors([correlationIdInterceptor, authInterceptor, errorInterceptor]),
    ),
    provideAnimations(),
    // Bottom-left, away from the NMate launcher in the opposite corner. Three at a time:
    // beyond that they stop being read and start being a wall. Errors set their own
    // timeOut: 0 at the call site, since a failure the reader missed is a failure twice.
    provideToastr({
      timeOut: 5000,
      positionClass: 'toast-bottom-left',
      preventDuplicates: true,
      progressBar: false,
      closeButton: true,
      maxOpened: 3,
      autoDismiss: true,
    }),
  ],
};
