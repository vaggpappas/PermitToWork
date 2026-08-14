import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { authInterceptor } from './core/auth/auth.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    // Pinned so month names are always English, whatever language the browser is set to.
    // Note this governs dates the application renders — the calendar popup inside
    // <input type="date"> is drawn by the browser in its own language, and no web
    // application can change that one.
    { provide: LOCALE_ID, useValue: 'en-GB' },

    // withComponentInputBinding lets a route parameter arrive as an @Input on the
    // component, so a detail page never has to subscribe to ActivatedRoute by hand.
    provideRouter(routes, withComponentInputBinding()),

    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
