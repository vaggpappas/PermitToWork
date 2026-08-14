import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { EmployeesApi } from '../../core/api/employees.api';
import { TeamsApi } from '../../core/api/teams.api';
import { AuthService, Roles } from '../../core/auth/auth.service';
import { EmployeeSummary, TeamDetail as Team, TeamRole } from '../../core/models';
import { describeError } from '../../core/problem-details';
import { SettingsService } from '../../core/settings/settings.service';

@Component({
  selector: 'app-team-detail',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './team-detail.html',
})
export class TeamDetail {
  private readonly teams = inject(TeamsApi);
  private readonly employees = inject(EmployeesApi);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(SettingsService);

  readonly id = input.required<string>();

  protected readonly team = signal<Team | null>(null);
  protected readonly candidates = signal<EmployeeSummary[]>([]);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly busy = signal(false);

  // Teams are changed by administrators and by employees marked Responsible. Everyone else
  // reads. The API enforces the same two roles — this only decides what is worth showing.
  protected readonly canManage = this.auth.hasAnyRole(Roles.Administrator, Roles.Responsible);
  protected readonly canDisband = this.auth.hasAnyRole(Roles.Administrator);

  protected readonly currentMembers = computed(() => this.team()?.members.filter((m) => m.isCurrent) ?? []);
  protected readonly pastMembers = computed(() => this.team()?.members.filter((m) => !m.isCurrent) ?? []);

  /** Anyone active who is not already on the crew. */
  protected readonly addableEmployees = computed(() => {
    const alreadyIn = new Set(this.currentMembers().map((m) => m.employeeId));
    return this.candidates().filter((employee) => !alreadyIn.has(employee.id));
  });

  protected readonly addingMember = signal(false);
  protected readonly memberForm = this.formBuilder.nonNullable.group({
    employeeId: ['', Validators.required],
    role: ['Member' as TeamRole, Validators.required],
  });

  constructor() {
    this.employees
      .search({ status: 'Active', pageSize: 100 })
      .subscribe({ next: (data) => this.candidates.set(data.items) });

    queueMicrotask(() => this.load());
  }

  protected load(): void {
    this.loading.set(true);

    this.teams.get(this.id()).subscribe({
      next: (data) => {
        this.team.set(data);
        this.loading.set(false);
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.loading.set(false);
      },
    });
  }

  protected addMember(): void {
    if (this.memberForm.invalid || this.busy()) {
      this.memberForm.markAllAsTouched();
      return;
    }

    const { employeeId, role } = this.memberForm.getRawValue();

    this.run(this.teams.addMember(this.id(), employeeId, role), () => {
      this.addingMember.set(false);
      this.memberForm.reset({ role: 'Member' });
    });
  }

  protected changeRole(employeeId: string, role: string): void {
    this.run(this.teams.changeMemberRole(this.id(), employeeId, role as TeamRole));
  }

  protected removeMember(employeeId: string, name: string): void {
    if (confirm(`Remove ${name} from this team? The membership is kept with a leaving date.`)) {
      this.run(this.teams.removeMember(this.id(), employeeId));
    }
  }

  protected disband(): void {
    if (confirm('Disband this team? Every remaining membership is ended today.')) {
      this.run(this.teams.disband(this.id()));
    }
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
