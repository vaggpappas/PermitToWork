import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { describeError } from '../../core/problem-details';
import { ThemeToggle } from '../../core/settings/theme-toggle';

type Mode = 'login' | 'register';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, ThemeToggle],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly mode = signal<Mode>('login');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    // Matches the API's Identity rules, so the common mistakes are caught before a round
    // trip. The server still enforces them — this only saves the user a wasted click.
    password: ['', [Validators.required, Validators.minLength(10)]],
  });

  protected switchTo(mode: Mode): void {
    this.mode.set(mode);
    this.error.set(null);
  }

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    const request =
      this.mode() === 'login' ? this.auth.login(email, password) : this.auth.register(email, password);

    this.busy.set(true);
    this.error.set(null);

    request.subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/employees';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (failure: unknown) => {
        this.error.set(describeError(failure));
        this.busy.set(false);
      },
    });
  }
}
