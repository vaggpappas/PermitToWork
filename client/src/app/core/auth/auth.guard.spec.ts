import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { expiringIn, jwtWith } from '../../../testing/jwt';
import { authGuard, roleGuard } from './auth.guard';
import { Roles } from './auth.service';

const TokenStorageKey = 'ptw.accessToken';

/**
 * A guard that lets the wrong person through is indistinguishable, at a glance, from one
 * that works — the screen simply opens. Nothing goes red until someone reports seeing a page
 * they should not have.
 *
 * What these deliberately do *not* claim is that the guards provide security. They do not,
 * and the file says so: anyone can edit local storage and reach the route. What they buy is
 * that a user is never sent to a screen whose every request is going to come back 403.
 */
describe('route guards', () => {
  beforeEach(() => {
    localStorage.removeItem(TokenStorageKey);

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  afterEach(() => localStorage.removeItem(TokenStorageKey));

  describe('authGuard', () => {
    it('lets a signed-in user through', () => {
      signedInAs({ role: Roles.Employee });

      expect(runAuthGuard('/permits')).toBe(true);
    });

    it('sends an anonymous user to the login screen', () => {
      expect(runAuthGuard('/permits')).toBeInstanceOf(UrlTree);
    });

    it('remembers where the user was headed', () => {
      const result = runAuthGuard('/permits/0195f2a1-0000-7000-8000-000000000001');

      // Without returnUrl, following a link from an email lands you on the employee list
      // after signing in, with no clue what you were sent to look at.
      expect(serialise(result)).toContain('returnUrl=');
      expect(decodeURIComponent(serialise(result))).toContain('/permits/0195f2a1-0000-7000-8000-000000000001');
    });

    it('turns away a user whose token has expired', () => {
      signedInAs({ role: Roles.Administrator }, expiringIn(-5));

      expect(runAuthGuard('/employees')).toBeInstanceOf(UrlTree);
    });
  });

  describe('roleGuard', () => {
    it('admits a user who holds the role', () => {
      signedInAs({ role: Roles.Administrator });

      expect(runRoleGuard(Roles.Administrator)).toBe(true);
    });

    it('turns away a user who does not', () => {
      signedInAs({ role: Roles.Employee });

      // This is the audit screen rule, checked from the other side: the API answers 403, and
      // the route never opens in the first place.
      const result = runRoleGuard(Roles.Administrator);

      expect(result).toBeInstanceOf(UrlTree);
      expect(serialise(result)).toContain('/employees');
    });

    it('sends an anonymous user to login, not to the fallback page', () => {
      const result = runRoleGuard(Roles.Administrator);

      // Two different failures deserve two different destinations. "You are not signed in"
      // is fixable by signing in; "you are not an administrator" is not, so bouncing an
      // anonymous visitor to /employees would hide the actual problem.
      expect(serialise(result)).toContain('/login');
    });

    it('admits a user holding any one of several roles', () => {
      signedInAs({ role: Roles.SafetyOfficer });

      expect(runRoleGuard(Roles.Administrator, Roles.SafetyOfficer)).toBe(true);
    });
  });

  function runAuthGuard(url: string): boolean | UrlTree {
    return TestBed.runInInjectionContext(
      () =>
        authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot) as boolean | UrlTree,
    );
  }

  function runRoleGuard(...roles: string[]): boolean | UrlTree {
    return TestBed.runInInjectionContext(
      () =>
        roleGuard(...roles)(
          {} as ActivatedRouteSnapshot,
          { url: '/admin/audit' } as RouterStateSnapshot,
        ) as boolean | UrlTree,
    );
  }

  function serialise(result: boolean | UrlTree): string {
    return result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : String(result);
  }

  function signedInAs(claims: Record<string, unknown>, exp = expiringIn(60)): void {
    localStorage.setItem(TokenStorageKey, jwtWith({ exp, ...claims }));
  }
});
