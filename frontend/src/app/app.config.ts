import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { providePrimeNG } from 'primeng/config';
import { ConfirmationService, MessageService } from 'primeng/api';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { TalmidonPreset } from './core/theme/talmidon-preset';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    MessageService,
    ConfirmationService,
    providePrimeNG({
      ripple: true,
      theme: {
        preset: TalmidonPreset,
        options: {
          // מצב כהה מופעל רק כשמוסיפים class .app-dark ל-<html>
          darkModeSelector: '.app-dark'
        }
      }
    })
  ]
};
