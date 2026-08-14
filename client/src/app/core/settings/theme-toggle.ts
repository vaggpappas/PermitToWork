import { Component, inject } from '@angular/core';
import { SettingsService } from './settings.service';

/**
 * Icon-only light/dark switch, sized to sit beside the signed-in name.
 *
 * Duplicated from the Settings page on purpose — the theme is the one preference people
 * change often enough to want without navigating for it. The label lives in `title` and
 * `aria-label` rather than on screen, so it stays available to a screen reader and on hover
 * without costing a line of sidebar.
 */
@Component({
  selector: 'app-theme-toggle',
  template: `
    <button
      type="button"
      class="theme-toggle"
      [attr.aria-label]="label()"
      [title]="label()"
      (click)="settings.toggleTheme()"
    >
      @if (settings.theme() === 'dark') {
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2m0 16v2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4M2 12h2m16 0h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
        </svg>
      } @else {
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z" />
        </svg>
      }
    </button>
  `,
  styles: `
    .theme-toggle {
      display: grid;
      place-items: center;
      width: 28px;
      height: 28px;
      padding: 0;
      border-radius: 8px;
      background: transparent;
      border: 1px solid transparent;
      color: var(--text-faint);
    }

    .theme-toggle:hover:not(:disabled) {
      background: var(--surface-raised);
      border-color: var(--border-strong);
      color: var(--text);
    }

    svg {
      width: 15px;
      height: 15px;
      fill: none;
      stroke: currentcolor;
      stroke-width: 1.9;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
  `,
})
export class ThemeToggle {
  protected readonly settings = inject(SettingsService);

  protected readonly label = () =>
    this.settings.theme() === 'dark' ? 'Switch to light theme' : 'Switch to dark theme';
}
