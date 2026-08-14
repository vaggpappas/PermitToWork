import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
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

  /** First letter of the email, for the avatar square. */
  protected readonly initial = computed(() => (this.auth.email() ?? '?').charAt(0).toUpperCase());

  /**
   * The role, spaced out for reading — "SafetyOfficer" is a claim value, not a label.
   * There is only ever one now that the role is a single field on the employee record.
   */
  protected readonly primaryRole = computed(() => {
    const role = this.auth.roles()[0];
    return role ? role.replace(/([a-z])([A-Z])/g, '$1 $2') : 'No role assigned';
  });
}
