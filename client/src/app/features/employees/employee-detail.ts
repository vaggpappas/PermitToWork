import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { EmployeesApi } from '../../core/api/employees.api';
import { LookupsApi } from '../../core/api/teams.api';
import { AuthService, Roles } from '../../core/auth/auth.service';
import {
  AccessRole,
  AccessRoles,
  EmployeeDetail as Employee,
  EmployeeSummary,
  Lookup,
  TeamSummary,
} from '../../core/models';
import { describeError } from '../../core/problem-details';
import { SettingsService } from '../../core/settings/settings.service';

@Component({
  selector: 'app-employee-detail',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './employee-detail.html',
})
export class EmployeeDetail {
  private readonly employees = inject(EmployeesApi);
  private readonly lookups = inject(LookupsApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(SettingsService);

  protected readonly accessRoles = AccessRoles;

  /** Bound from the route by withComponentInputBinding — no ActivatedRoute subscription. */
  readonly id = input.required<string>();

  protected readonly employee = signal<Employee | null>(null);
  protected readonly teams = signal<TeamSummary[]>([]);
  protected readonly trades = signal<Lookup[]>([]);
  protected readonly certificationTypes = signal<Lookup[]>([]);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly canManage = this.auth.hasAnyRole(Roles.Administrator, Roles.Supervisor);
  protected readonly canAdminister = this.auth.hasAnyRole(Roles.Administrator);
  protected readonly canCertify = this.auth.hasAnyRole(Roles.Administrator, Roles.SafetyOfficer);

  /** Only supervisors and administrators decide what somebody may do. */
  protected readonly canAssignRole = this.auth.hasAnyRole(Roles.Administrator, Roles.Supervisor);

  protected readonly editing = signal(false);
  protected readonly editForm = this.formBuilder.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: '',
    jobTitle: ['', Validators.required],
    tradeId: ['', Validators.required],
    dateOfBirth: '',
    street: '',
    city: '',
    postalCode: '',
    country: '',
  });

  /**
   * The reporting line.
   *
   * This searches rather than listing everybody, and that is not a stylistic choice: the
   * employee endpoint clamps its page size to 100, so a plain dropdown would silently omit
   * the 101st person and look complete while doing it. A search box cannot mislead in that
   * way — an empty result says "no match", not "no such colleague".
   */
  protected readonly editingManager = signal(false);
  protected readonly managerCandidates = signal<EmployeeSummary[]>([]);
  protected readonly managerSearched = signal(false);
  protected readonly managerForm = this.formBuilder.nonNullable.group({
    search: '',
    managerId: '',
  });

  protected readonly addingCertification = signal(false);
  protected readonly certificationForm = this.formBuilder.nonNullable.group({
    certificationTypeId: ['', Validators.required],
    issuedBy: ['', Validators.required],
    issuedOn: ['', Validators.required],
    expiresOn: ['', Validators.required],
    referenceNumber: '',
  });

  constructor() {
    this.lookups.trades().subscribe({ next: (data) => this.trades.set(data) });
    this.lookups.certificationTypes().subscribe({ next: (data) => this.certificationTypes.set(data) });

    // input.required is populated before the first render, so reading it here is safe.
    queueMicrotask(() => this.load());
  }

  protected load(): void {
    this.loading.set(true);

    this.employees.get(this.id()).subscribe({
      next: (data) => {
        this.employee.set(data);
        this.loading.set(false);
        this.fillEditForm(data);
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.loading.set(false);
      },
    });

    this.employees.teams(this.id()).subscribe({ next: (data) => this.teams.set(data) });
  }

  protected startEditing(): void {
    const current = this.employee();
    if (current) {
      this.fillEditForm(current);
    }
    this.editing.set(true);
  }

  protected save(): void {
    if (this.editForm.invalid || this.busy()) {
      this.editForm.markAllAsTouched();
      return;
    }

    const value = this.editForm.getRawValue();

    // The API takes the whole address or none of it — a half-filled one is rejected by the
    // domain, so an untouched street means no address rather than four empty strings.
    const address = value.street && value.city && value.postalCode && value.country
      ? {
          street: value.street,
          city: value.city,
          postalCode: value.postalCode,
          country: value.country,
        }
      : null;

    this.run(
      this.employees.update(this.id(), {
        firstName: value.firstName,
        lastName: value.lastName,
        email: value.email,
        phoneNumber: value.phoneNumber || null,
        jobTitle: value.jobTitle,
        tradeId: value.tradeId,
        dateOfBirth: value.dateOfBirth || null,
        address,
      }),
      () => this.editing.set(false),
    );
  }

  protected assignAccessRole(role: string): void {
    this.run(this.employees.assignAccessRole(this.id(), role as AccessRole));
  }

  protected openManagerEditor(): void {
    this.managerForm.reset();
    this.managerCandidates.set([]);
    this.managerSearched.set(false);
    this.editingManager.set(true);

    // Open with the first page already listed. Making the user press Search against an empty
    // box to discover there are colleagues at all is a worse first impression than showing
    // some, and it costs one request either way.
    this.findManagers();
  }

  protected findManagers(): void {
    this.employees
      .search({
        search: this.managerForm.controls.search.value,
        // Only people still employed here. Someone who left last year is not a reporting
        // line, and offering them invites a mistake the server would have to refuse.
        status: 'Active',
        pageSize: 50,
      })
      .subscribe({
        next: (page) => {
          // The server refuses a self-assignment anyway; removing it from the list means the
          // user never gets to make a choice that is going to be rejected.
          this.managerCandidates.set(page.items.filter((candidate) => candidate.id !== this.id()));
          this.managerSearched.set(true);
        },
        error: (failure: unknown) => this.actionError.set(describeError(failure)),
      });
  }

  protected saveManager(): void {
    const managerId = this.managerForm.controls.managerId.value;
    if (!managerId) {
      return;
    }

    this.run(this.employees.assignManager(this.id(), managerId), () => this.editingManager.set(false));
  }

  /** Null is the payload, not an omission — it is how the API says "reports to nobody". */
  protected clearManager(): void {
    this.run(this.employees.assignManager(this.id(), null), () => this.editingManager.set(false));
  }

  protected suspend(): void {
    this.run(this.employees.suspend(this.id()));
  }

  protected reinstate(): void {
    this.run(this.employees.reinstate(this.id()));
  }

  protected terminate(): void {
    if (confirm('End this person’s employment? The record is kept, not deleted.')) {
      this.run(this.employees.terminate(this.id()));
    }
  }

  protected addCertification(): void {
    if (this.certificationForm.invalid || this.busy()) {
      this.certificationForm.markAllAsTouched();
      return;
    }

    const value = this.certificationForm.getRawValue();

    this.run(
      this.employees.addCertification(this.id(), {
        ...value,
        referenceNumber: value.referenceNumber || null,
      }),
      () => {
        this.addingCertification.set(false);
        this.certificationForm.reset();
      },
    );
  }

  protected removeCertification(certificationId: string): void {
    this.run(this.employees.removeCertification(this.id(), certificationId));
  }

  /**
   * Every command does the same three things — disable the UI, reload on success, show the
   * message on failure. Written once so that a new action is one line, and so no action
   * can quietly forget to re-enable the buttons.
   */
  private run(request: Observable<unknown>, onSuccess?: () => void): void {
    this.busy.set(true);
    this.actionError.set(null);

    request.subscribe({
      next: () => {
        this.busy.set(false);
        onSuccess?.();
        this.load();
      },
      error: (failure: unknown) => {
        this.actionError.set(describeError(failure));
        this.busy.set(false);
      },
    });
  }

  private fillEditForm(employee: Employee): void {
    this.editForm.setValue({
      firstName: employee.firstName,
      lastName: employee.lastName,
      email: employee.email,
      phoneNumber: employee.phoneNumber ?? '',
      jobTitle: employee.jobTitle,
      tradeId: employee.tradeId,
      dateOfBirth: employee.dateOfBirth ?? '',
      street: employee.address?.street ?? '',
      city: employee.address?.city ?? '',
      postalCode: employee.address?.postalCode ?? '',
      country: employee.address?.country ?? '',
    });
  }
}
