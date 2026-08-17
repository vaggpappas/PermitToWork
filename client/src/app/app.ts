import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, Roles } from './core/auth/auth.service';
import { CurrentEmployeeService } from './core/auth/current-employee.service';
import { SettingsService } from './core/settings/settings.service';
import { ThemeToggle } from './core/settings/theme-toggle';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ThemeToggle],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(SettingsService);
  protected readonly current = inject(CurrentEmployeeService);

  /**
   * Who is offered the search-by-person view.
   *
   * A computed, not a plain field: the sidebar is drawn before anyone signs in and has to
   * redraw when they do. The same list is on the route guard and on the API, and the API is
   * the one that decides — this only keeps a link off the screen that could not work.
   */
  protected readonly canSearchCrews = computed(() =>
    this.auth.hasAnyRole(Roles.Administrator, Roles.Supervisor, Roles.SafetyOfficer, Roles.Responsible),
  );

  /** First letter of the displayed name, for the avatar square. */
  protected readonly initial = computed(() => this.current.displayName().charAt(0).toUpperCase());

  /**
   * The role, spaced out for reading — "SafetyOfficer" is a claim value, not a label.
   * There is only ever one now that the role is a single field on the employee record.
   */
  protected readonly primaryRole = computed(() => {
    const role = this.auth.roles()[0];
    return role ? role.replace(/([a-z])([A-Z])/g, '$1 $2') : 'No role assigned';
  });
}
