import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthenticationResponse } from '../models';

/** The claims this application issues. Names match the API exactly. */
interface JwtClaims {
  sub?: string;
  email?: string;
  role?: string | string[];
  exp?: number;
  'ptw:employee_id'?: string;
  'ptw:company_id'?: string;
  'ptw:scope'?: string;
}

/**
 * Mirrors the AccessRole enum on the server. A person holds exactly one — it is a field on
 * their employee record, not a list of memberships.
 */
export const Roles = {
  Administrator: 'Administrator',
  SafetyOfficer: 'SafetyOfficer',
  Supervisor: 'Supervisor',
  Responsible: 'Responsible',
  Employee: 'Employee',
} as const;

const TokenStorageKey = 'ptw.accessToken';

/**
 * Sign-in state, derived from the bearer token.
 *
 * Everything public here is a signal or computed from one, because Angular is zoneless:
 * assigning to a plain field would change the value without anything re-rendering.
 *
 * Note what this class does *not* do — it never decides whether an action is permitted.
 * The role checks below drive what the UI bothers to show. The server decides what is
 * allowed, every time, because a token is just a string a browser can be told to lie about.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly accessToken = signal<string | null>(localStorage.getItem(TokenStorageKey));

  private readonly claims = computed<JwtClaims | null>(() => {
    const token = this.accessToken();
    return token ? decodeJwt(token) : null;
  });

  /** Read by the interceptor on every outgoing request. */
  readonly token = this.accessToken.asReadonly();

  readonly isAuthenticated = computed(() => {
    const claims = this.claims();
    if (!claims?.exp) {
      return false;
    }

    // Expiry is checked here as well as on the server, so an obviously dead token sends
    // the user to the login screen instead of collecting a 401 on the next click.
    return claims.exp * 1000 > Date.now();
  });

  readonly email = computed(() => this.claims()?.email ?? null);

  readonly employeeId = computed(() => this.claims()?.['ptw:employee_id'] ?? null);

  readonly roles = computed<string[]>(() => {
    const role = this.claims()?.role;
    if (!role) {
      return [];
    }
    return Array.isArray(role) ? role : [role];
  });

  readonly seesAllCompanies = computed(() => this.claims()?.['ptw:scope'] === 'all');

  readonly isAdministrator = computed(() => this.hasAnyRole(Roles.Administrator));

  /** True when the user holds at least one of the given roles. */
  hasAnyRole(...roles: string[]): boolean {
    const held = this.roles();
    return roles.some((role) => held.includes(role));
  }

  login(email: string, password: string): Observable<AuthenticationResponse> {
    return this.http
      .post<AuthenticationResponse>('/api/auth/login', { email, password })
      .pipe(tap((response) => this.store(response.accessToken)));
  }

  register(email: string, password: string): Observable<AuthenticationResponse> {
    return this.http
      .post<AuthenticationResponse>('/api/auth/register', { email, password })
      .pipe(tap((response) => this.store(response.accessToken)));
  }

  logout(): void {
    localStorage.removeItem(TokenStorageKey);
    this.accessToken.set(null);
    void this.router.navigate(['/login']);
  }

  private store(token: string): void {
    localStorage.setItem(TokenStorageKey, token);
    this.accessToken.set(token);
  }
}

/**
 * Reads the payload of a JWT. Deliberately does not verify the signature — a browser
 * cannot, and does not need to: the token is only useful against an API that checks it
 * properly. Anything read here is for deciding what to render.
 */
function decodeJwt(token: string): JwtClaims | null {
  try {
    const payload = token.split('.')[1];
    if (!payload) {
      return null;
    }

    // base64url → base64, then restore the padding atob insists on.
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');

    // Via bytes rather than atob's output directly, so non-ASCII names survive.
    const bytes = Uint8Array.from(atob(padded), (character) => character.charCodeAt(0));
    return JSON.parse(new TextDecoder().decode(bytes)) as JwtClaims;
  } catch {
    return null;
  }
}
