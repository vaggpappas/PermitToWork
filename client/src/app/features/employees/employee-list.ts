import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EmployeesApi } from '../../core/api/employees.api';
import { LookupsApi } from '../../core/api/teams.api';
import { AuthService, Roles } from '../../core/auth/auth.service';
import { EmployeeSummary, Lookup, PagedResult } from '../../core/models';
import { describeError } from '../../core/problem-details';

@Component({
  selector: 'app-employee-list',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './employee-list.html',
})
export class EmployeeList {
  private readonly employees = inject(EmployeesApi);
  private readonly lookups = inject(LookupsApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly auth = inject(AuthService);

  protected readonly result = signal<PagedResult<EmployeeSummary> | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly companies = signal<Lookup[]>([]);
  protected readonly trades = signal<Lookup[]>([]);

  protected readonly filters = this.formBuilder.nonNullable.group({
    search: '',
    status: '',
    companyId: '',
  });

  protected readonly page = signal(1);

  // Creating employees is an administrator's job, so the panel is not even rendered for
  // anyone else. The API refuses regardless — this only avoids offering a dead end.
  protected readonly canCreate = this.auth.hasAnyRole(Roles.Administrator);
  protected readonly showCreate = signal(false);
  protected readonly saving = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly createForm = this.formBuilder.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: '',
    companyId: ['', Validators.required],
    tradeId: ['', Validators.required],
    jobTitle: ['', Validators.required],
    hireDate: [new Date().toISOString().slice(0, 10), Validators.required],
    dateOfBirth: '',
  });

  constructor() {
    this.load();

    this.lookups.companies().subscribe({ next: (data) => this.companies.set(data) });
    this.lookups.trades().subscribe({ next: (data) => this.trades.set(data) });
  }

  protected load(): void {
    const { search, status, companyId } = this.filters.getRawValue();

    this.loading.set(true);
    this.error.set(null);

    this.employees
      .search({
        search,
        status: (status || undefined) as never,
        companyId: companyId || undefined,
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
    // A new filter means a new result set, so page 1 — otherwise a search from page 4
    // lands on an empty page and looks like it found nothing.
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

    this.employees
      .create({
        ...value,
        phoneNumber: value.phoneNumber || null,
        dateOfBirth: value.dateOfBirth || null,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.showCreate.set(false);
          this.createForm.reset({ hireDate: new Date().toISOString().slice(0, 10) });
          this.applyFilters();
        },
        error: (failure: unknown) => {
          this.createError.set(describeError(failure));
          this.saving.set(false);
        },
      });
  }
}
