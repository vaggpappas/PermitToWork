import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PermitsApi } from '../../core/api/permits.api';
import { AuthService, Roles } from '../../core/auth/auth.service';
import { PagedResult, PermitStatus, PermitSummary, PermitType } from '../../core/models';
import { describeError } from '../../core/problem-details';
import { SettingsService } from '../../core/settings/settings.service';

type Scope = 'all' | 'mine' | 'awaiting';

@Component({
  selector: 'app-permit-list',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './permit-list.html',
  styleUrl: './permit-list.scss',
})
export class PermitList {
  private readonly permits = inject(PermitsApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(SettingsService);

  protected readonly result = signal<PagedResult<PermitSummary> | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly permitTypes = signal<PermitType[]>([]);
  protected readonly page = signal(1);

  /**
   * Three views of the same list rather than three checkboxes. "Waiting for me" is the
   * question an approver opens this screen to answer, so it deserves to be one click and
   * not a filter combination.
   */
  protected readonly scope = signal<Scope>('all');

  protected readonly filters = this.formBuilder.nonNullable.group({
    search: '',
    status: '',
    permitTypeId: '',
  });

  protected readonly canCreate = this.auth.hasAnyRole(
    Roles.Administrator,
    Roles.Supervisor,
    Roles.Responsible,
  );

  /** Permits whose window has closed while still unfinished. Worth surfacing on its own. */
  protected readonly overdueCount = computed(
    () => this.result()?.items.filter((permit) => permit.isOverdue).length ?? 0,
  );

  constructor() {
    this.load();
    this.permits.permitTypes().subscribe({ next: (types) => this.permitTypes.set(types) });
  }

  protected load(): void {
    const { search, status, permitTypeId } = this.filters.getRawValue();
    const scope = this.scope();

    this.loading.set(true);
    this.error.set(null);

    this.permits
      .search({
        search,
        status: (status || undefined) as PermitStatus | undefined,
        permitTypeId: permitTypeId || undefined,
        awaitingMyApproval: scope === 'awaiting',
        raisedByMe: scope === 'mine',
        page: this.page(),
        pageSize: 20,
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

  protected showScope(scope: Scope): void {
    this.scope.set(scope);
    this.page.set(1);
    this.load();
  }

  protected applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  protected goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }
}
