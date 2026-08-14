import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EmployeesApi } from '../../core/api/employees.api';
import { LookupsApi, TeamsApi } from '../../core/api/teams.api';
import { AuthService, Roles } from '../../core/auth/auth.service';
import { EmployeeSummary, Lookup, PagedResult, TeamSummary } from '../../core/models';
import { describeError } from '../../core/problem-details';

@Component({
  selector: 'app-team-list',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './team-list.html',
})
export class TeamList {
  private readonly teamsApi = inject(TeamsApi);
  private readonly employees = inject(EmployeesApi);
  private readonly lookups = inject(LookupsApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly auth = inject(AuthService);

  protected readonly result = signal<PagedResult<TeamSummary> | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly facilities = signal<Lookup[]>([]);
  protected readonly candidateLeaders = signal<EmployeeSummary[]>([]);

  protected readonly filters = this.formBuilder.nonNullable.group({
    search: '',
    status: '',
  });

  protected readonly page = signal(1);

  protected readonly canCreate = this.auth.hasAnyRole(Roles.Administrator, Roles.Responsible);
  protected readonly showCreate = signal(false);
  protected readonly saving = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly createForm = this.formBuilder.nonNullable.group({
    name: ['', Validators.required],
    description: '',
    facilityId: ['', Validators.required],
    // Required by the API: a team is created together with its leader, so a member-less
    // team never exists — which matters because team visibility runs through membership.
    leaderEmployeeId: ['', Validators.required],
  });

  constructor() {
    this.load();

    this.lookups.facilities().subscribe({ next: (data) => this.facilities.set(data) });

    this.employees
      .search({ status: 'Active', pageSize: 100 })
      .subscribe({ next: (data) => this.candidateLeaders.set(data.items) });
  }

  protected load(): void {
    const { search, status } = this.filters.getRawValue();

    this.loading.set(true);
    this.error.set(null);

    this.teamsApi
      .search({
        search,
        status: (status || undefined) as never,
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

  protected applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  protected goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  protected create(): void {
    if (this.createForm.invalid || this.saving()) {
      this.createForm.markAllAsTouched();
      return;
    }

    const value = this.createForm.getRawValue();

    this.saving.set(true);
    this.createError.set(null);

    this.teamsApi
      .create({ ...value, description: value.description || null })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.showCreate.set(false);
          this.createForm.reset();
          this.applyFilters();
        },
        error: (failure: unknown) => {
          this.createError.set(describeError(failure));
          this.saving.set(false);
        },
      });
  }
}
