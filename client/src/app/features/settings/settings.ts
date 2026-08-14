import { Component, inject } from '@angular/core';
import {
  Accent,
  DateFormat,
  Density,
  FontChoice,
  SettingsService,
  TextSize,
  Theme,
} from '../../core/settings/settings.service';

interface Choice<T> {
  value: T;
  label: string;
  hint?: string;
}

@Component({
  selector: 'app-settings',
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class Settings {
  protected readonly settings = inject(SettingsService);

  // Data rather than markup: adding a fifth accent is one line here plus one CSS block,
  // and the template never changes.
  protected readonly themes: Choice<Theme>[] = [
    { value: 'light', label: 'Light', hint: 'For daylight and printed handovers' },
    { value: 'dark', label: 'Dark', hint: 'Easier at night and in a control room' },
  ];

  protected readonly accents: Choice<Accent>[] = [
    { value: 'violet', label: 'Violet' },
    { value: 'blue', label: 'Blue' },
    { value: 'emerald', label: 'Emerald' },
    { value: 'amber', label: 'Amber' },
  ];

  protected readonly fonts: Choice<FontChoice>[] = [
    { value: 'inter', label: 'Inter', hint: 'Default' },
    { value: 'system', label: 'System', hint: 'Whatever your OS uses' },
    { value: 'serif', label: 'Serif', hint: 'Higher contrast for long reading' },
    { value: 'mono', label: 'Monospace', hint: 'Badge numbers and codes line up' },
  ];

  protected readonly textSizes: Choice<TextSize>[] = [
    { value: 'small', label: 'Small' },
    { value: 'medium', label: 'Medium' },
    { value: 'large', label: 'Large' },
  ];

  protected readonly densities: Choice<Density>[] = [
    { value: 'comfortable', label: 'Comfortable', hint: 'More breathing room' },
    { value: 'compact', label: 'Compact', hint: 'More rows on screen' },
  ];

  protected readonly dateFormats: Choice<DateFormat>[] = [
    { value: 'd MMM yyyy', label: '5 Aug 2026', hint: 'Unambiguous — the month is spelled' },
    { value: 'dd/MM/yyyy', label: '05/08/2026', hint: 'Day first, as in Greece and the UK' },
    { value: 'yyyy-MM-dd', label: '2026-08-05', hint: 'ISO 8601 — sorts correctly as text' },
  ];
}
