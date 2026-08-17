import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EmployeesApi } from '../../core/api/employees.api';
import { AuthService } from '../../core/auth/auth.service';
import { CurrentEmployeeService } from '../../core/auth/current-employee.service';
import { EmployeeDetail, TeamSummary } from '../../core/models';
import { describeError } from '../../core/problem-details';
import { SettingsService } from '../../core/settings/settings.service';

/**
 * Your own record.
 *
 * Almost everything here is read-only, and that is the design rather than an omission. Trade
 * and job title decide which permits a person may be added to; letting somebody edit their
 * own would let them widen what they are allowed to do on site. The server enforces this by
 * having no field for it — the page just explains why.
 */
@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './profile.html',
})
export class Profile {
  private readonly employees = inject(EmployeesApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly current = inject(CurrentEmployeeService);

  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(SettingsService);

  protected readonly me = signal<EmployeeDetail | null>(null);
  protected readonly teams = signal<TeamSummary[]>([]);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly busy = signal(false);
  protected readonly editing = signal(false);

  /** Certificates that have run out, so the warning is impossible to scroll past. */
  protected readonly expired = computed(
    () => this.me()?.certifications.filter((certification) => !certification.isValid) ?? [],
  );

  protected readonly form = this.formBuilder.nonNullable.group({
    phoneNumber: '',
    street: '',
    city: '',
    postalCode: '',
    country: '',
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.employees.me().subscribe({
      next: (employee) => {
        this.me.set(employee);
        this.fill(employee);
        this.loading.set(false);

        this.employees.teams(employee.id).subscribe({ next: (data) => this.teams.set(data) });
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.loading.set(false);
      },
    });
  }

  protected startEditing(): void {
    const employee = this.me();
    if (employee) {
      this.fill(employee);
    }
    this.saved.set(false);
    this.editing.set(true);
  }

  protected save(): void {
    if (this.busy()) {
      return;
    }

    const value = this.form.getRawValue();

    // All four address fields or none: the domain refuses a half-filled address, so sending
    // one would be asking for a 422 we already know the answer to.
    const address =
      value.street && value.city && value.postalCode && value.country
        ? {
            street: value.street,
            city: value.city,
            postalCode: value.postalCode,
            country: value.country,
          }
        : null;

    this.busy.set(true);
    this.saveError.set(null);

    this.employees
      .updateMyContact({ phoneNumber: value.phoneNumber || null, address })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.editing.set(false);
          this.saved.set(true);
          this.load();

          // The sidebar reads the same record. Without this it would keep showing whatever
          // it loaded at sign-in until the next full page refresh.
          this.current.reload();
        },
        error: (failure: unknown) => {
          this.saveError.set(describeError(failure));
          this.busy.set(false);
        },
      });
  }

  private fill(employee: EmployeeDetail): void {
    this.form.setValue({
      phoneNumber: employee.phoneNumber ?? '',
      street: employee.address?.street ?? '',
      city: employee.address?.city ?? '',
      postalCode: employee.address?.postalCode ?? '',
      country: employee.address?.country ?? '',
    });
  }
}
