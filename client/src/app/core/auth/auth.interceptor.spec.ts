import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { expiringIn, jwtWith } from '../../../testing/jwt';
import { authInterceptor } from './auth.interceptor';
import { AuthService, Roles } from './auth.service';

const TokenStorageKey = 'ptw.accessToken';

/**
 * Exercised through a real HttpClient rather than by calling the function directly, because
 * the thing worth proving is that the interceptor is actually *wired in* — a correct
 * interceptor that nobody registered behaves exactly like no interceptor at all.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    localStorage.removeItem(TokenStorageKey);
  });

  afterEach(() => localStorage.removeItem(TokenStorageKey));

  it('attaches the bearer token to API calls', () => {
    signedIn();

    http.get('/api/employees').subscribe();

    const request = backend.expectOne('/api/employees');
    expect(request.request.headers.get('Authorization')).toMatch(/^Bearer /);
  });

  it('sends nothing when the user is signed out', () => {
    start();

    http.get('/api/health').subscribe();

    expect(backend.expectOne('/api/health').request.headers.has('Authorization')).toBe(false);
  });

  it('does not leak the token to other hosts', () => {
    signedIn();

    http.get('https://example.com/anything').subscribe();

    // The token is a credential for this API and nobody else. Attaching it to every outgoing
    // request would hand it to whichever third party the application talks to next.
    expect(backend.expectOne('https://example.com/anything').request.headers.has('Authorization'))
      .toBe(false);
  });

  it('signs the user out when the API rejects the token', () => {
    signedIn();
    const auth = TestBed.inject(AuthService);

    http.get('/api/employees').subscribe({ error: () => undefined });
    backend.expectOne('/api/employees').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.token()).toBeNull();
  });

  it('keeps the user signed in when they are merely not allowed', () => {
    signedIn();
    const auth = TestBed.inject(AuthService);

    http.get('/api/audit').subscribe({ error: () => undefined });
    backend.expectOne('/api/audit').flush(null, { status: 403, statusText: 'Forbidden' });

    // This is the distinction the whole interceptor turns on. A plain employee opening the
    // audit screen gets 403 — their token is perfectly valid, they simply may not read it.
    // Signing them out here would look like a random logout with no explanation.
    expect(auth.token()).not.toBeNull();
  });

  it('passes the error on rather than swallowing it', () => {
    signedIn();
    let seen: unknown = null;

    http.get('/api/permits').subscribe({ error: (failure: unknown) => (seen = failure) });
    backend.expectOne('/api/permits').flush(
      { detail: 'Only a Draft permit can be edited.' },
      { status: 422, statusText: 'Unprocessable Content' },
    );

    // The screen still has to show the reason. An interceptor that handles an error into
    // silence leaves the user pressing a button that does nothing.
    expect(seen).not.toBeNull();
  });

  function start(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        // The 401 case signs the user out, and logout() navigates to /login. Without a route
        // to land on, that navigation rejects and vitest reports an unhandled error.
        provideRouter([{ path: 'login', children: [] }]),
      ],
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  }

  function signedIn(): void {
    localStorage.setItem(TokenStorageKey, jwtWith({ role: Roles.Employee, exp: expiringIn(60) }));
    start();
  }
});
