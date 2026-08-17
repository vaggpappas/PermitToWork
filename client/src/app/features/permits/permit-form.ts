import { Component, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { EmployeesApi } from '../../core/api/employees.api';
import { PermitsApi } from '../../core/api/permits.api';
import { LookupsApi } from '../../core/api/teams.api';
import { EmployeeSummary, Lookup, PermitType } from '../../core/models';
import { describeError } from '../../core/problem-details';

@Component({
  selector: 'app-permit-form',
  imports: [ReactiveFormsModule],
  templateUrl: './permit-form.html',
  styleUrl: './permit-form.scss',
})
export class PermitForm {
  private readonly permits = inject(PermitsApi);
  private readonly lookups = inject(LookupsApi);
  private readonly employees = inject(EmployeesApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);

  /** Present when editing a draft, absent when raising a new permit. */
  readonly id = input<string | undefined>(undefined);

  protected readonly permitTypes = signal<PermitType[]>([]);
  protected readonly categories = signal<Lookup[]>([]);
  protected readonly facilities = signal<Lookup[]>([]);
  protected readonly buildings = signal<Lookup[]>([]);
  protected readonly locations = signal<Lookup[]>([]);
  protected readonly people = signal<EmployeeSummary[]>([]);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly isEdit = computed(() => this.id() !== undefined);

  protected readonly form = this.formBuilder.nonNullable.group({
    permitTypeId: ['', Validators.required],
    categoryId: ['', Validators.required],
    workDescription: ['', [Validators.required, Validators.minLength(10)]],
    facilityId: ['', Validators.required],
    buildingId: ['', Validators.required],
    locationId: ['', Validators.required],
    validFrom: ['', Validators.required],
    validTo: ['', Validators.required],
    receiverId: ['', Validators.required],
    project: '',
    notes: '',
  });

  /**
   * What the chosen permit type demands of everyone on the crew. Shown while the permit is
   * still being written, so the author picks a type knowing what it will cost them — rather
   * than discovering it when the first worker is refused.
   */
  protected readonly requiredCertifications = computed(() => {
    const chosen = this.permitTypes().find((type) => type.id === this.form.controls.permitTypeId.value);
    return chosen?.requiredCertifications ?? [];
  });

  constructor() {
    this.permits.permitTypes().subscribe({ next: (types) => this.permitTypes.set(types) });
    this.permits.categories().subscribe({ next: (groups) => this.categories.set(groups) });
    this.lookups.facilities().subscribe({ next: (list) => this.facilities.set(list) });
    this.employees
      .search({ status: 'Active', pageSize: 100 })
      .subscribe({ next: (page) => this.people.set(page.items) });

    queueMicrotask(() => {
      if (this.id()) {
        this.loadExisting();
      }
    });
  }

  protected onFacilityChange(facilityId: string): void {
    this.form.patchValue({ buildingId: '', locationId: '' });
    this.buildings.set([]);
    this.locations.set([]);

    if (facilityId) {
      this.lookups.buildings(facilityId).subscribe({ next: (list) => this.buildings.set(list) });
    }
  }

  protected onBuildingChange(buildingId: string): void {
    this.form.patchValue({ locationId: '' });
    this.locations.set([]);

    if (buildingId) {
      this.lookups.locations(buildingId).subscribe({ next: (list) => this.locations.set(list) });
    }
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    // datetime-local gives a string with no zone. Through Date and back out as ISO so the
    // server receives an unambiguous instant rather than "8am, somewhere".
    const body = {
      categoryId: value.categoryId,
      workDescription: value.workDescription,
      locationId: value.locationId,
      validFrom: new Date(value.validFrom).toISOString(),
      validTo: new Date(value.validTo).toISOString(),
      receiverId: value.receiverId,
      project: value.project || null,
      notes: value.notes || null,
    };

    this.saving.set(true);
    this.error.set(null);

    // Branched rather than assigned to one variable. Create returns the new id and update
    // returns nothing, so a union of the two observables has no single subscribe signature
    // TypeScript can choose — and casting the result away would only hide that.
    const existing = this.id();

    if (existing) {
      this.permits.update(existing, body).subscribe({
        next: () => this.done(existing),
        error: (failure: unknown) => this.fail(failure),
      });
    } else {
      this.permits.create({ ...body, permitTypeId: value.permitTypeId }).subscribe({
        next: (created) => this.done(created.id),
        error: (failure: unknown) => this.fail(failure),
      });
    }
  }

  private done(permitId: string): void {
    this.saving.set(false);
    void this.router.navigate(['/permits', permitId]);
  }

  private fail(failure: unknown): void {
    this.error.set(describeError(failure));
    this.saving.set(false);
  }

  protected cancel(): void {
    const existing = this.id();
    void this.router.navigate(existing ? ['/permits', existing] : ['/permits']);
  }

  private loadExisting(): void {
    this.permits.get(this.id()!).subscribe({
      next: (permit) => {
        this.lookups.buildings(permit.facilityId).subscribe({ next: (list) => this.buildings.set(list) });

        this.form.patchValue({
          permitTypeId: permit.permitTypeId,
          categoryId: permit.categoryId,
          workDescription: permit.workDescription,
          facilityId: permit.facilityId,
          locationId: permit.locationId,
          validFrom: toLocalInput(permit.validFrom),
          validTo: toLocalInput(permit.validTo),
          receiverId: permit.receiverId,
          project: permit.project ?? '',
          notes: permit.notes ?? '',
        });

        // The permit type cannot change after the permit exists — its requirements were
        // snapshotted, and the number already carries the type code.
        this.form.controls.permitTypeId.disable();
      },
      error: (failure: unknown) => this.error.set(describeError(failure)),
    });
  }
}

/** ISO instant to the "YYYY-MM-DDTHH:mm" a datetime-local input expects, in local time. */
function toLocalInput(iso: string): string {
  const date = new Date(iso);
  const offsetMinutes = date.getTimezoneOffset();

  return new Date(date.getTime() - offsetMinutes * 60_000).toISOString().slice(0, 16);
}
