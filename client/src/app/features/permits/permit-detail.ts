import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { EmployeesApi } from '../../core/api/employees.api';
import { PermitsApi } from '../../core/api/permits.api';
import { AuthService } from '../../core/auth/auth.service';
import { EmployeeSummary, PermitDetail as Permit } from '../../core/models';
import { describeError } from '../../core/problem-details';
import { SettingsService } from '../../core/settings/settings.service';

@Component({
  selector: 'app-permit-detail',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './permit-detail.html',
  styleUrl: './permit-detail.scss',
})
export class PermitDetail {
  private readonly permits = inject(PermitsApi);
  private readonly employees = inject(EmployeesApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(SettingsService);

  readonly id = input.required<string>();

  protected readonly permit = signal<Permit | null>(null);
  protected readonly candidates = signal<EmployeeSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly addingWorker = signal(false);
  protected readonly addingEquipment = signal(false);

  protected readonly workerForm = this.formBuilder.nonNullable.group({
    employeeId: ['', Validators.required],
    note: '',
  });

  protected readonly equipmentForm = this.formBuilder.nonNullable.group({
    description: ['', Validators.required],
    identifier: '',
    quantity: [1, [Validators.required, Validators.min(1)]],
  });

  /** People not already on the crew. */
  protected readonly addableEmployees = computed(() => {
    const onCrew = new Set(this.permit()?.workers.map((worker) => worker.employeeId) ?? []);
    return this.candidates().filter((person) => !onCrew.has(person.id));
  });

  /**
   * Whether the signed-in person has an outstanding signature on this permit.
   *
   * Approving is not a role — it is a seat on this permit's panel, captured when it was
   * submitted. So the button appears based on the approvals actually on the permit, which
   * is the same thing the server checks.
   */
  protected readonly myApproval = computed(() => {
    const me = this.auth.employeeId();
    if (!me) {
      return null;
    }

    return this.permit()?.approvals.find(
      (approval) => approval.approverEmployeeId === me && approval.decision === 'Pending',
    ) ?? null;
  });

  /** Closing is the creator's job, so the button only appears for them. */
  protected readonly isCreator = computed(() => this.permit()?.createdById === this.auth.employeeId());

  constructor() {
    this.employees
      .search({ status: 'Active', pageSize: 100 })
      .subscribe({ next: (page) => this.candidates.set(page.items) });

    queueMicrotask(() => this.load());
  }

  protected load(): void {
    this.loading.set(true);

    this.permits.get(this.id()).subscribe({
      next: (data) => {
        this.permit.set(data);
        this.loading.set(false);
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.loading.set(false);
      },
    });
  }

  /* ------------------------------------------------------------ resources */

  protected addWorker(): void {
    if (this.workerForm.invalid || this.busy()) {
      this.workerForm.markAllAsTouched();
      return;
    }

    const { employeeId, note } = this.workerForm.getRawValue();

    // A refusal here is the certification rule firing. It arrives as a 422 with the
    // person's name and the missing certificate, which is exactly what the user needs.
    this.run(this.permits.addWorker(this.id(), employeeId, note || null), () => {
      this.addingWorker.set(false);
      this.workerForm.reset();
    });
  }

  protected removeWorker(employeeId: string, name: string): void {
    if (confirm(`Take ${name} off this permit?`)) {
      this.run(this.permits.removeWorker(this.id(), employeeId));
    }
  }

  protected addEquipment(): void {
    if (this.equipmentForm.invalid || this.busy()) {
      this.equipmentForm.markAllAsTouched();
      return;
    }

    const value = this.equipmentForm.getRawValue();

    this.run(
      this.permits.addEquipment(this.id(), { ...value, identifier: value.identifier || null }),
      () => {
        this.addingEquipment.set(false);
        this.equipmentForm.reset({ quantity: 1 });
      },
    );
  }

  protected removeEquipment(equipmentId: string): void {
    this.run(this.permits.removeEquipment(this.id(), equipmentId));
  }

  /* ------------------------------------------------------------ lifecycle */

  protected submit(): void {
    this.run(this.permits.submit(this.id()));
  }

  protected approve(): void {
    const comment = prompt('Any comment to record with your approval? (optional)');

    // prompt returns null when cancelled and "" when confirmed empty. Only the former
    // means "I changed my mind".
    if (comment === null) {
      return;
    }

    this.run(this.permits.approve(this.id(), comment || null));
  }

  protected reject(): void {
    const reason = this.askForReason('Why are you refusing this permit?');
    if (reason) {
      this.run(this.permits.reject(this.id(), reason));
    }
  }

  protected suspend(): void {
    const reason = this.askForReason('Why is the work being stopped?');
    if (reason) {
      this.run(this.permits.suspend(this.id(), reason));
    }
  }

  protected resume(): void {
    this.run(this.permits.resume(this.id()));
  }

  protected close(): void {
    const note = prompt('Any closing note? (optional)');
    if (note === null) {
      return;
    }

    this.run(this.permits.close(this.id(), note || null));
  }

  protected cancel(): void {
    const reason = this.askForReason('Why is this permit being called off?');
    if (reason) {
      this.run(this.permits.cancel(this.id(), reason));
    }
  }

  /** Reasons are mandatory on the server, so an empty one is refused before the round trip. */
  private askForReason(question: string): string | null {
    const answer = prompt(question)?.trim();

    if (answer === undefined) {
      return null;
    }

    if (answer.length < 3) {
      this.actionError.set('A reason is required, and needs to be more than a couple of characters.');
      return null;
    }

    return answer;
  }

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
}
