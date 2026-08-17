import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EmployeesApi } from '../../core/api/employees.api';
import { PermitsApi } from '../../core/api/permits.api';
import { EmployeeSummary, PermitStatus, PermitSummary } from '../../core/models';
import { describeError } from '../../core/problem-details';
import { SettingsService } from '../../core/settings/settings.service';

/**
 * Where is this person working?
 *
 * The permit list already holds these facts, but organised by permit. During an incident the
 * question runs the other way — somebody names a person and needs to know what they are on —
 * and answering it by opening permits one at a time is exactly the wrong thing to be doing at
 * that moment.
 */
@Component({
  selector: 'app-crew-search',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './crew-search.html',
})
export class CrewSearch {
  private readonly employees = inject(EmployeesApi);
  private readonly permits = inject(PermitsApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly settings = inject(SettingsService);

  protected readonly candidates = signal<EmployeeSummary[]>([]);
  protected readonly searched = signal(false);

  protected readonly chosen = signal<EmployeeSummary | null>(null);
  protected readonly results = signal<PermitSummary[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({ search: '' });

  /**
   * Live work by default.
   *
   * "Where is Marta right now" is the question this page exists for, and it is asked far more
   * often than "what was she on in March". The filter widens to everything in one click, so
   * the rarer question costs one extra action rather than the common one costing it forever.
   */
  protected readonly liveOnly = signal(true);

  protected findPeople(): void {
    this.employees.search({ search: this.form.controls.search.value, status: 'Active', pageSize: 50 }).subscribe({
      next: (page) => {
        this.candidates.set(page.items);
        this.searched.set(true);
      },
      error: (failure: unknown) => this.error.set(describeError(failure)),
    });
  }

  protected choose(employee: EmployeeSummary): void {
    this.chosen.set(employee);
    this.loadPermits();
  }

  protected toggleScope(): void {
    this.liveOnly.set(!this.liveOnly());
    if (this.chosen()) {
      this.loadPermits();
    }
  }

  protected loadPermits(): void {
    const employee = this.chosen();
    if (!employee) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.permits.assignedTo(employee.id, { order: 'Schedule', pageSize: 100 }).subscribe({
      next: (page) => {
        // Filtered here rather than by asking the server for one status, because "live" is
        // two statuses and a clock, and the status filter on the API takes exactly one value.
        this.results.set(this.liveOnly() ? page.items.filter(isLive) : page.items);
        this.loading.set(false);
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.loading.set(false);
      },
    });
  }

  protected clear(): void {
    this.chosen.set(null);
    this.results.set([]);
  }
}

const LiveStatuses: PermitStatus[] = ['Active', 'Pending', 'Suspended'];

/** On the books now: not yet finished, and the clock is inside the window. */
function isLive(permit: PermitSummary): boolean {
  const now = Date.now();

  return (
    LiveStatuses.includes(permit.status) &&
    new Date(permit.validFrom).getTime() <= now &&
    now < new Date(permit.validTo).getTime()
  );
}
