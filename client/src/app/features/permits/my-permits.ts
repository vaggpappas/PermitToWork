import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PermitsApi } from '../../core/api/permits.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermitSummary } from '../../core/models';
import { describeError } from '../../core/problem-details';
import { SettingsService } from '../../core/settings/settings.service';

/**
 * What the signed-in person is actually on — the one screen a worker needs.
 *
 * Everything else in the application answers "what permits exist". A fitter does not want
 * that. They want to know what they are on today and what is coming, which is why the server
 * sorts this by schedule rather than newest-first.
 */
@Component({
  selector: 'app-my-permits',
  // NgTemplateOutlet so the three sections share one table definition. Repeating the markup
  // three times would mean a column added to one of them and forgotten in the others.
  imports: [RouterLink, DatePipe, NgTemplateOutlet],
  templateUrl: './my-permits.html',
})
export class MyPermits {
  private readonly permits = inject(PermitsApi);

  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(SettingsService);

  protected readonly items = signal<PermitSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly showFinished = signal(false);

  /**
   * Split for the template rather than filtered in it, so "is this permit on right now" is
   * decided in one place. The comparison is half-open — the end instant is the first moment
   * outside the period — matching DateTimeRange.Contains on the server.
   */
  protected readonly live = computed(() => this.items().filter((permit) => isLive(permit)));
  protected readonly upcoming = computed(() =>
    this.items().filter((permit) => !isLive(permit) && !isFinished(permit)),
  );
  protected readonly finished = computed(() => this.items().filter(isFinished));

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.permits
      .search({ assignedToMe: true, order: 'Schedule', pageSize: 100 })
      .subscribe({
        next: (page) => {
          this.items.set(page.items);
          this.loading.set(false);
        },
        error: (failure: unknown) => {
          this.error.set(describeError(failure));
          this.loading.set(false);
        },
      });
  }
}

/** Live work: approved, and the clock is inside the window. */
function isLive(permit: PermitSummary): boolean {
  const now = Date.now();

  return (
    permit.status === 'Active' &&
    new Date(permit.validFrom).getTime() <= now &&
    now < new Date(permit.validTo).getTime()
  );
}

/** Nothing further will happen to it. */
function isFinished(permit: PermitSummary): boolean {
  return (
    permit.status === 'Closed' ||
    permit.status === 'Cancelled' ||
    permit.status === 'Rejected' ||
    permit.status === 'Expired'
  );
}
