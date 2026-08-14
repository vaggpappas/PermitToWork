import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Attaches the bearer token to every API call, and signs the user out if the API ever
 * rejects it.
 *
 * One place, so no component ever builds an Authorization header — which is what stops the
 * one screen that forgot from being a mystery bug months later.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.token();

  const authorised =
    token && request.url.startsWith('/api')
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorised).pipe(
    catchError((error: unknown) => {
      // 401 means the token is missing, expired or invalid — sign out and let the guard
      // send them to the login screen. A 403 is different: the token is fine, the user
      // simply may not do this, so signing them out would be both wrong and infuriating.
      if (error instanceof HttpErrorResponse && error.status === 401 && auth.token()) {
        auth.logout();
      }

      return throwError(() => error);
    }),
  );
};
