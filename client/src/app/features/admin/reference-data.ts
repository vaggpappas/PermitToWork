import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { PlaceBody, ReferenceAdminApi } from '../../core/api/admin.api';
import { ReferenceItem, ReferenceKind } from '../../core/models';
import { describeError } from '../../core/problem-details';

interface Tab {
  kind: ReferenceKind;
  label: string;
  /** The route segment the create and rename endpoints live under, where there is one. */
  route?: 'companies' | 'facilities' | 'buildings' | 'locations' | 'trades' | 'certification-types' | 'categories';
  hint: string;
  /** Buildings need a facility, locations need a building. */
  parentKind?: ReferenceKind;
  parentLabel?: string;
}

@Component({
  selector: 'app-reference-data',
  imports: [ReactiveFormsModule],
  templateUrl: './reference-data.html',
  styleUrl: './reference-data.scss',
})
export class ReferenceDataAdmin {
  private readonly api = inject(ReferenceAdminApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly tabs: Tab[] = [
    {
      kind: 'Facility',
      label: 'Facilities',
      route: 'facilities',
      hint: 'Sites. Each one has its own approval panel, and permits are raised against a place inside it.',
    },
    {
      kind: 'Building',
      label: 'Buildings',
      route: 'buildings',
      parentKind: 'Facility',
      parentLabel: 'Facility',
      hint: 'Units and areas within a facility. Codes may repeat across sites — UNIT3 at two refineries is two places.',
    },
    {
      kind: 'Location',
      label: 'Locations',
      route: 'locations',
      parentKind: 'Building',
      parentLabel: 'Building',
      hint: 'Rooms, bays, closets — the finest granularity work is located at.',
    },
    {
      kind: 'Company',
      label: 'Companies',
      route: 'companies',
      hint: 'Employers. Contractor staff only ever see their own company’s records.',
    },
    {
      kind: 'Trade',
      label: 'Trades',
      route: 'trades',
      hint: 'The craft an employee practises. Rule-bearing — a hot work permit wants a welder.',
    },
    {
      kind: 'CertificationType',
      label: 'Certification types',
      route: 'certification-types',
      hint: 'Qualifications people hold, and what permit types demand of them.',
    },
    {
      kind: 'Category',
      label: 'Categories',
      route: 'categories',
      hint: 'Why the work is happening — maintenance, inspection, construction.',
    },
    {
      kind: 'PermitType',
      label: 'Permit types',
      hint: 'The kinds of hazardous work, and the certifications each one requires of every worker.',
    },
  ];

  protected readonly tab = signal<Tab>(this.tabs[0]);
  protected readonly items = signal<ReferenceItem[]>([]);
  protected readonly parents = signal<ReferenceItem[]>([]);
  protected readonly certificationTypes = signal<ReferenceItem[]>([]);

  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showCreate = signal(false);

  /** Set when a row is being renamed in place. */
  protected readonly editingId = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(20)]],
    name: ['', Validators.required],
    description: '',
    parentId: '',
    companyKind: 'Contractor',
    requiredCertificationTypeIds: [[] as string[]],
  });

  protected readonly editForm = this.formBuilder.nonNullable.group({
    name: ['', Validators.required],
    description: '',
    requiredCertificationTypeIds: [[] as string[]],
  });

  protected readonly needsParent = computed(() => this.tab().parentKind !== undefined);

  protected readonly parentChosen = computed(
    () => !this.needsParent() || this.form.controls.parentId.value !== '',
  );

  constructor() {
    this.load();
    this.api
      .list('CertificationType')
      .subscribe({ next: (types) => this.certificationTypes.set(types.filter((t) => t.isActive)) });
  }

  protected select(tab: Tab): void {
    this.tab.set(tab);
    this.showCreate.set(false);
    this.editingId.set(null);
    this.error.set(null);
    this.form.reset({ companyKind: 'Contractor', requiredCertificationTypeIds: [] });
    this.load();
  }

  protected load(): void {
    const tab = this.tab();

    this.loading.set(true);
    this.error.set(null);

    if (tab.parentKind) {
      this.api.list(tab.parentKind).subscribe({ next: (list) => this.parents.set(list) });
    }

    this.api.list(tab.kind).subscribe({
      next: (list) => {
        this.items.set(list);
        this.loading.set(false);
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.loading.set(false);
      },
    });
  }

  protected toggleRequirement(certificationTypeId: string, checked: boolean, edit = false): void {
    const control = edit
      ? this.editForm.controls.requiredCertificationTypeIds
      : this.form.controls.requiredCertificationTypeIds;

    const current = control.value;
    control.setValue(
      checked ? [...current, certificationTypeId] : current.filter((id) => id !== certificationTypeId),
    );
  }

  protected isRequired(certificationTypeId: string, edit = false): boolean {
    const control = edit
      ? this.editForm.controls.requiredCertificationTypeIds
      : this.form.controls.requiredCertificationTypeIds;

    return control.value.includes(certificationTypeId);
  }

  protected create(): void {
    if (this.form.invalid || this.busy() || !this.parentChosen()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const place: PlaceBody = {
      code: value.code,
      name: value.name,
      description: value.description || null,
    };

    let request: Observable<{ id: string }>;

    switch (this.tab().kind) {
      case 'Company':
        request = this.api.createCompany({ code: value.code, name: value.name, kind: value.companyKind });
        break;
      case 'Facility':
        request = this.api.createFacility(place);
        break;
      case 'Building':
        request = this.api.createBuilding(value.parentId, place);
        break;
      case 'Location':
        request = this.api.createLocation(value.parentId, place);
        break;
      case 'PermitType':
        request = this.api.createPermitType({
          code: value.code,
          name: value.name,
          description: value.description || null,
          requiredCertificationTypeIds: value.requiredCertificationTypeIds,
        });
        break;
      default:
        request = this.api.createLookup(
          this.tab().route as 'trades' | 'certification-types' | 'categories',
          { code: value.code, name: value.name },
        );
    }

    this.run(request, () => {
      this.showCreate.set(false);
      this.form.reset({ companyKind: 'Contractor', requiredCertificationTypeIds: [] });
    });
  }

  protected startEditing(item: ReferenceItem): void {
    this.editingId.set(item.id);
    this.editForm.setValue({
      name: item.name,
      description: item.description ?? '',
      // The names are stored as a joined string for display; matched back to ids here.
      requiredCertificationTypeIds: this.certificationTypes()
        .filter((type) => (item.extra ?? '').split(', ').includes(type.name))
        .map((type) => type.id),
    });
  }

  protected saveEdit(id: string): void {
    if (this.editForm.invalid || this.busy()) {
      this.editForm.markAllAsTouched();
      return;
    }

    const value = this.editForm.getRawValue();
    const kind = this.tab().kind;

    let request: Observable<void>;

    if (kind === 'PermitType') {
      request = this.api.updatePermitType(id, {
        name: value.name,
        description: value.description || null,
        requiredCertificationTypeIds: value.requiredCertificationTypeIds,
      });
    } else if (kind === 'Facility' || kind === 'Building' || kind === 'Location') {
      request = this.api.updatePlace(this.tab().route as 'facilities' | 'buildings' | 'locations', id, {
        name: value.name,
        description: value.description || null,
      });
    } else {
      request = this.api.rename(
        this.tab().route as 'companies' | 'trades' | 'certification-types' | 'categories',
        id,
        value.name,
      );
    }

    this.run(request, () => this.editingId.set(null));
  }

  protected setActive(item: ReferenceItem, isActive: boolean): void {
    this.run(this.api.setActive(this.tab().kind, item.id, isActive));
  }

  protected parentName(parentId: string | null): string {
    return this.parents().find((parent) => parent.id === parentId)?.name ?? '—';
  }

  private run(request: Observable<unknown>, onSuccess?: () => void): void {
    this.busy.set(true);
    this.error.set(null);

    request.subscribe({
      next: () => {
        this.busy.set(false);
        onSuccess?.();
        this.load();
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.busy.set(false);
      },
    });
  }
}
