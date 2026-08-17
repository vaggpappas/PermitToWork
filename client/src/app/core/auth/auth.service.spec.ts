import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { expiringIn, jwtWith } from '../../../testing/jwt';
import { AuthService, Roles } from './auth.service';

const TokenStorageKey = 'ptw.accessToken';

/**
 * The chain this covers is the one nothing else can see: Employee.AccessRole on the server
 * becomes a "role" claim, the claim is read back here, and what the sidebar draws depends on
 * the answer. Every link is a string, so every link fails silently — a renamed claim gives an
 * empty roles array, which looks exactly like "this user is a plain employee".
 */
describe('AuthService', () => {
  beforeEach(() => {
    localStorage.removeItem(TokenStorageKey);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // A real /login route, because logout() navigates there. With an empty route table
        // the navigation rejects with NG04002 and vitest reports an unhandled error that has
        // nothing to do with what is being tested.
        provideRouter([{ path: 'login', children: [] }]),
      ],
    });
  });

  afterEach(() => localStorage.removeItem(TokenStorageKey));

  it('starts signed out when there is no token', () => {
    const auth = TestBed.inject(AuthService);

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.roles()).toEqual([]);
    expect(auth.email()).toBeNull();
  });

  it('restores the session from local storage', () => {
    // Set before injecting: the service reads storage once, in a field initialiser. This is
    // also what makes a page refresh keep you signed in.
    signedInAs({ role: Roles.Administrator });

    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(true);
  });

  it('stores the token that login returns', () => {
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);

    auth.login('admin@permittowork.local', 'Admin!23456').subscribe();

    const request = http.expectOne('/api/auth/login');
    expect(request.request.body).toEqual({
      email: 'admin@permittowork.local',
      password: 'Admin!23456',
    });

    request.flush({
      accessToken: jwtWith({ email: 'admin@permittowork.local', role: Roles.Administrator, exp: expiringIn(60) }),
      expiresAtUtc: new Date().toISOString(),
    });

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.email()).toBe('admin@permittowork.local');

    // Written to storage too, or a refresh would sign them straight back out.
    expect(localStorage.getItem(TokenStorageKey)).not.toBeNull();
  });

  it('treats an expired token as signed out', () => {
    signedInAs({ role: Roles.Administrator }, expiringIn(-1));

    // Checked in the browser as well as on the server, so a dead token sends the user to the
    // login screen instead of collecting a 401 on their next click.
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
  });

  it('reads a single role claim as a list of one', () => {
    signedInAs({ role: Roles.SafetyOfficer });
    const auth = TestBed.inject(AuthService);

    // The API writes one role as a bare string and several as an array. Both have to arrive
    // here as an array, or hasAnyRole quietly compares against characters.
    expect(auth.roles()).toEqual([Roles.SafetyOfficer]);
    expect(auth.hasAnyRole(Roles.SafetyOfficer)).toBe(true);
  });

  it('reads several role claims as a list', () => {
    signedInAs({ role: [Roles.Supervisor, Roles.Responsible] });
    const auth = TestBed.inject(AuthService);

    expect(auth.roles()).toEqual([Roles.Supervisor, Roles.Responsible]);
    expect(auth.hasAnyRole(Roles.Administrator, Roles.Supervisor)).toBe(true);
  });

  it('refuses a role the user does not hold', () => {
    signedInAs({ role: Roles.Employee });
    const auth = TestBed.inject(AuthService);

    expect(auth.hasAnyRole(Roles.Administrator)).toBe(false);
    expect(auth.isAdministrator()).toBe(false);
  });

  it('reads the company scope and the employee id', () => {
    signedInAs({
      role: Roles.Administrator,
      'ptw:scope': 'all',
      'ptw:employee_id': '0195f2a1-0000-7000-8000-000000000001',
    });
    const auth = TestBed.inject(AuthService);

    expect(auth.seesAllCompanies()).toBe(true);
    expect(auth.employeeId()).toBe('0195f2a1-0000-7000-8000-000000000001');
  });

  it('scopes a contractor to their own company', () => {
    signedInAs({ role: Roles.Employee, 'ptw:scope': '0195f2a1-0000-7000-8000-0000000000aa' });

    // Anything other than "all" is one company. The UI uses this only to word its hints;
    // the query filter on the server is what actually restricts the rows.
    expect(TestBed.inject(AuthService).seesAllCompanies()).toBe(false);
  });

  it('survives a name that is not ASCII', () => {
    signedInAs({ role: Roles.Employee, email: 'βαγγέλης@παράδειγμα.gr' });

    // decodeJwt goes through TextDecoder rather than using atob's output directly. Without
    // that, this address comes back as mojibake and the header shows nonsense.
    expect(TestBed.inject(AuthService).email()).toBe('βαγγέλης@παράδειγμα.gr');
  });

  it('treats a corrupt token as no token', () => {
    localStorage.setItem(TokenStorageKey, 'this-is-not-a-jwt');

    // Whatever is in storage came from outside the application's control. Throwing here
    // would white-screen the app on a value the user can edit by hand.
    const auth = TestBed.inject(AuthService);

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.roles()).toEqual([]);
  });

  it('clears everything on sign-out', () => {
    signedInAs({ role: Roles.Administrator });
    const auth = TestBed.inject(AuthService);

    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token()).toBeNull();
    expect(localStorage.getItem(TokenStorageKey)).toBeNull();
  });

  function signedInAs(claims: Record<string, unknown>, exp = expiringIn(60)): void {
    localStorage.setItem(TokenStorageKey, jwtWith({ exp, ...claims }));
  }
});
