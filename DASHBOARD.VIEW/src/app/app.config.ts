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
    provideToastr({
      timeOut: 4000,
      positionClass: 'toast-top-right',
      preventDuplicates: true,
      progressBar: true,
      closeButton: true,
    }),
  ],
};
