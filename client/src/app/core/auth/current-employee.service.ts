import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { EmployeesApi } from '../api/employees.api';
import { AuthService } from './auth.service';
import { EmployeeDetail } from '../models';

/**
 * The signed-in user's employee record, loaded once and shared.
 *
 * Separate from AuthService on purpose. AuthService derives everything from the token and
 * nothing else — that is what makes it testable without a server, and what keeps it honest
 * about only knowing what the token says. A person's name is not in the token, and it should
 * not be: a token is issued at sign-in and never changes, so a name embedded in one would go
 * stale the moment they corrected the spelling of it and would stay stale until they signed
 * out. Fetching it means `reload()` after a save updates the sidebar immediately.
 */
@Injectable({ providedIn: 'root' })
export class CurrentEmployeeService {
  private readonly employees = inject(EmployeesApi);
  private readonly auth = inject(AuthService);

  private readonly record = signal<EmployeeDetail | null>(null);

  readonly employee = this.record.asReadonly();

  /**
   * Falls back to the email until the record arrives, and again if it never does.
   *
   * A sidebar that renders empty for a moment on every page load looks broken, and one that
   * renders empty forever after a failed request looks worse.
   */
  readonly displayName = computed(() => this.record()?.fullName ?? this.auth.email() ?? 'Signed in');

  constructor() {
    // Tied to the token rather than called from a component: signing in and signing out both
    // have to change this, and neither of them goes through the profile page.
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.reload();
      } else {
        this.record.set(null);
      }
    });
  }

  reload(): void {
    this.employees.me().subscribe({
      next: (employee) => this.record.set(employee),
      // Deliberately silent. This drives a name in the corner of the screen; failing to load
      // it is not something to interrupt the user about, and displayName already copes.
      error: () => this.record.set(null),
    });
  }
}
