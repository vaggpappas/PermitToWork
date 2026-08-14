import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Keeps unauthenticated users out, remembering where they were headed. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/**
 * Restricts a route to certain roles.
 *
 * Worth being clear about what this is for: hiding a screen the user cannot use, so they
 * are not sent to a page that can only fail. It is not a security control — anyone can
 * edit the token in local storage and reach the route. The API checks the same roles on
 * every request, and that check is the one that counts.
 */
export const roleGuard = (...roles: string[]): CanActivateFn => {
  return (_route, _state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }

    return auth.hasAnyRole(...roles) ? true : router.createUrlTree(['/employees']);
  };
};
