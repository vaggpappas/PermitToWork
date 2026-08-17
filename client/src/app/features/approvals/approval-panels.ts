import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { EmployeesApi } from '../../core/api/employees.api';
import { FacilityApproversApi } from '../../core/api/permits.api';
import { LookupsApi } from '../../core/api/teams.api';
import { AuthService, Roles } from '../../core/auth/auth.service';
import { EmployeeSummary, FacilityApprover, Lookup } from '../../core/models';
import { describeError } from '../../core/problem-details';

@Component({
  selector: 'app-approval-panels',
  imports: [ReactiveFormsModule],
  templateUrl: './approval-panels.html',
  styleUrl: './approval-panels.scss',
})
export class ApprovalPanels {
  private readonly panels = inject(FacilityApproversApi);
  private readonly lookups = inject(LookupsApi);
  private readonly employees = inject(EmployeesApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly auth = inject(AuthService);

  protected readonly facilities = signal<Lookup[]>([]);
  protected readonly selectedFacilityId = signal<string | null>(null);
  protected readonly panel = signal<FacilityApprover[]>([]);
  protected readonly candidates = signal<EmployeeSummary[]>([]);

  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly canManage = this.auth.hasAnyRole(Roles.Administrator);

  protected readonly selectedFacility = computed(() =>
    this.facilities().find((facility) => facility.id === this.selectedFacilityId()) ?? null,
  );

  /** Anybody not already seated on this panel. */
  protected readonly addableEmployees = computed(() => {
    const seated = new Set(this.panel().map((approver) => approver.employeeId));
    return this.candidates().filter((person) => !seated.has(person.id));
  });

  /**
   * A facility with nobody on its panel cannot have permits submitted at all — Submit
   * refuses, because there would be nobody to approve them. Worth saying loudly on the one
   * screen that can fix it.
   */
  protected readonly hasNoApprovers = computed(
    () => this.selectedFacilityId() !== null && !this.loading() && this.panel().length === 0,
  );

  protected readonly form = this.formBuilder.nonNullable.group({
    employeeId: ['', Validators.required],
    isDecisive: false,
  });

  constructor() {
    this.lookups.facilities().subscribe({
      next: (facilities) => {
        this.facilities.set(facilities);

        // Land on the first facility rather than an empty screen with a dropdown.
        if (facilities.length > 0) {
          this.select(facilities[0].id);
        }
      },
      error: (failure: unknown) => this.error.set(describeError(failure)),
    });

    this.employees
      .search({ status: 'Active', pageSize: 100 })
      .subscribe({ next: (page) => this.candidates.set(page.items) });
  }

  protected select(facilityId: string): void {
    this.selectedFacilityId.set(facilityId);
    this.loadPanel();
  }

  protected loadPanel(): void {
    const facilityId = this.selectedFacilityId();
    if (!facilityId) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.panels.panel(facilityId).subscribe({
      next: (approvers) => {
        this.panel.set(approvers);
        this.loading.set(false);
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.loading.set(false);
      },
    });
  }

  protected add(): void {
    const facilityId = this.selectedFacilityId();

    if (this.form.invalid || this.busy() || !facilityId) {
      this.form.markAllAsTouched();
      return;
    }

    const { employeeId, isDecisive } = this.form.getRawValue();

    this.run(this.panels.add(facilityId, employeeId, isDecisive), () =>
      this.form.reset({ isDecisive: false }),
    );
  }

  protected setDecisive(approverId: string, isDecisive: boolean): void {
    const facilityId = this.selectedFacilityId();
    if (facilityId) {
      this.run(this.panels.setDecisive(facilityId, approverId, isDecisive));
    }
  }

  protected remove(approver: FacilityApprover): void {
    const facilityId = this.selectedFacilityId();

    if (
      facilityId &&
      confirm(
        `Take ${approver.employeeName} off this panel? ` +
          'Permits already submitted keep their copy of this seat.',
      )
    ) {
      this.run(this.panels.remove(facilityId, approver.id));
    }
  }

  private run(request: Observable<unknown>, onSuccess?: () => void): void {
    this.busy.set(true);
    this.error.set(null);

    request.subscribe({
      next: () => {
        this.busy.set(false);
        onSuccess?.();
        this.loadPanel();
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.busy.set(false);
      },
    });
  }
}
