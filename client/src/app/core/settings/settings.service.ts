import { Injectable, effect, signal } from '@angular/core';

export type Theme = 'light' | 'dark';
export type Accent = 'violet' | 'blue' | 'emerald' | 'amber';
export type FontChoice = 'inter' | 'system' | 'serif' | 'mono';
export type TextSize = 'small' | 'medium' | 'large';
export type Density = 'comfortable' | 'compact';
export type DateFormat = 'd MMM yyyy' | 'dd/MM/yyyy' | 'yyyy-MM-dd';

interface Preferences {
  theme: Theme;
  accent: Accent;
  font: FontChoice;
  textSize: TextSize;
  density: Density;
  dateFormat: DateFormat;
  accentUserName: boolean;
}

const StorageKey = 'ptw.settings';

const Defaults: Preferences = {
  theme: 'dark',
  accent: 'violet',
  font: 'inter',
  textSize: 'medium',
  density: 'comfortable',
  dateFormat: 'd MMM yyyy',
  accentUserName: true,
};

/**
 * Everything the user can change about how the application looks.
 *
 * Each preference becomes a `data-*` attribute on `<html>`, and the stylesheet defines
 * what each value means. Nothing here knows a colour, a pixel or a font name — which is
 * why adding a fifth accent later is one block of CSS and one entry in a list, with no
 * TypeScript change at all.
 *
 * The one exception is `dateFormat`, which components pass to Angular's DatePipe. A date
 * format cannot be expressed as a CSS variable.
 */
@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly preferences = signal<Preferences>(load());

  readonly theme = signal<Theme>(this.preferences().theme);
  readonly accent = signal<Accent>(this.preferences().accent);
  readonly font = signal<FontChoice>(this.preferences().font);
  readonly textSize = signal<TextSize>(this.preferences().textSize);
  readonly density = signal<Density>(this.preferences().density);
  readonly dateFormat = signal<DateFormat>(this.preferences().dateFormat);
  readonly accentUserName = signal<boolean>(this.preferences().accentUserName);

  constructor() {
    effect(() => {
      const current: Preferences = {
        theme: this.theme(),
        accent: this.accent(),
        font: this.font(),
        textSize: this.textSize(),
        density: this.density(),
        dateFormat: this.dateFormat(),
        accentUserName: this.accentUserName(),
      };

      const root = document.documentElement;
      root.dataset['theme'] = current.theme;
      root.dataset['accent'] = current.accent;
      root.dataset['font'] = current.font;
      root.dataset['text'] = current.textSize;
      root.dataset['density'] = current.density;
      root.dataset['accentName'] = String(current.accentUserName);

      localStorage.setItem(StorageKey, JSON.stringify(current));
    });
  }

  toggleTheme(): void {
    this.theme.update((theme) => (theme === 'dark' ? 'light' : 'dark'));
  }

  /** Puts every preference back to the shipped default. */
  resetToDefaults(): void {
    this.theme.set(Defaults.theme);
    this.accent.set(Defaults.accent);
    this.font.set(Defaults.font);
    this.textSize.set(Defaults.textSize);
    this.density.set(Defaults.density);
    this.dateFormat.set(Defaults.dateFormat);
    this.accentUserName.set(Defaults.accentUserName);
  }
}

/**
 * Stored preferences win; anything missing falls back to the default, so adding a new
 * preference later does not break the saved settings of anyone already using the app.
 * The theme in particular defaults to the operating system rather than to dark.
 */
function load(): Preferences {
  const systemTheme: Theme = window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  const defaults: Preferences = { ...Defaults, theme: systemTheme };

  try {
    const stored = localStorage.getItem(StorageKey);
    return stored ? { ...defaults, ...(JSON.parse(stored) as Partial<Preferences>) } : defaults;
  } catch {
    return defaults;
  }
}
