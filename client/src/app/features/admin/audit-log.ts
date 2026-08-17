import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AuditApi } from '../../core/api/admin.api';
import { AuditEntry, PagedResult } from '../../core/models';
import { describeError } from '../../core/problem-details';
import { SettingsService } from '../../core/settings/settings.service';

/** One property that changed, flattened out of the stored JSON for display. */
interface Change {
  property: string;
  from: string;
  to: string;
}

@Component({
  selector: 'app-audit-log',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './audit-log.html',
  styleUrl: './audit-log.scss',
})
export class AuditLog {
  private readonly api = inject(AuditApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly settings = inject(SettingsService);

  protected readonly result = signal<PagedResult<AuditEntry> | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);

  /** Rows are collapsed by default; the change detail is the noisy part. */
  protected readonly expanded = signal<Set<string>>(new Set());

  protected readonly filters = this.formBuilder.nonNullable.group({
    search: '',
    action: '',
    entityType: '',
    from: '',
    to: '',
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    const { search, action, entityType, from, to } = this.filters.getRawValue();

    this.loading.set(true);
    this.error.set(null);

    this.api
      .search({
        search,
        action: action || undefined,
        entityType: entityType || undefined,
        // datetime-local gives a zoneless string; through Date so the server gets an instant.
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to).toISOString() : undefined,
        page: this.page(),
        pageSize: 25,
      })
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.loading.set(false);
        },
        error: (failure: unknown) => {
          this.error.set(describeError(failure));
          this.loading.set(false);
        },
      });
  }

  protected applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  protected goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  protected toggle(id: string): void {
    this.expanded.update((current) => {
      const next = new Set(current);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  protected isExpanded(id: string): boolean {
    return this.expanded().has(id);
  }

  /**
   * Turns the stored JSON into rows.
   *
   * An update stores `{"Status":{"from":"Active","to":"Suspended"}}`; a create or delete
   * stores plain values. Both are flattened to the same shape so the template has one case
   * rather than three.
   */
  protected changesOf(entry: AuditEntry): Change[] {
    if (!entry.changes) {
      return [];
    }

    try {
      const parsed = JSON.parse(entry.changes) as Record<string, unknown>;

      return Object.entries(parsed).map(([property, value]) => {
        if (value !== null && typeof value === 'object' && 'from' in value) {
          const pair = value as { from: unknown; to: unknown };
          return { property, from: display(pair.from), to: display(pair.to) };
        }

        return { property, from: '', to: display(value) };
      });
    } catch {
      // Stored JSON is truncated at 4000 characters, so a very large change can arrive
      // unparseable. Showing the raw text beats showing nothing.
      return [{ property: 'raw', from: '', to: entry.changes }];
    }
  }
}

function display(value: unknown): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}
