import { Component, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
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

  /**
   * Whether the password is shown as text.
   *
   * Never remembered between visits. A preference like the theme is worth persisting; "show
   * my password" is not, because the next person to open this laptop would inherit it.
   */
  protected readonly passwordVisible = signal(false);

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      email: ['', [Validators.required, Validators.email]],
      // Matches the API's Identity rules, so the common mistakes are caught before a round
      // trip. The server still enforces them — this only saves the user a wasted click.
      password: ['', [Validators.required, Validators.minLength(10)]],
      confirmPassword: '',
    },
    { validators: passwordsMatch },
  );

  /**
   * Whether the two passwords differ, once there is something to compare.
   *
   * Read from the group rather than recomputed in the template, so the message and the
   * disabled state can never disagree about whether the form is usable.
   */
  protected readonly mismatch = computed(() => this.formErrors()?.['passwordsDiffer'] === true);

  /** Signal mirror of the group's errors, because a FormGroup is not reactive on its own. */
  private readonly formErrors = signal<ValidationErrors | null>(null);

  constructor() {
    this.form.statusChanges.subscribe(() => this.formErrors.set(this.form.errors));
  }

  protected switchTo(mode: Mode): void {
    this.mode.set(mode);
    this.error.set(null);

    // Hidden again when the form changes purpose. Switching to Register is a new intent, and
    // carrying a revealed password across it is a small surprise nobody asked for.
    this.passwordVisible.set(false);

    // The second box exists only when registering. Left required in login mode it would
    // block a sign-in over a field that is not on the screen — a form that refuses to submit
    // and will not say why.
    const confirm = this.form.controls.confirmPassword;
    confirm.setValue('');

    if (mode === 'register') {
      confirm.setValidators([Validators.required]);
    } else {
      confirm.clearValidators();
    }

    confirm.updateValueAndValidity();
  }

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    // Only the two the API takes. The confirmation never leaves the browser — it exists to
    // catch a typo, and sending it would invite a server that starts trusting it.
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

/**
 * The two password boxes have to agree.
 *
 * A group validator rather than one on the field, because "do these match" is a fact about
 * the pair — a validator on `confirmPassword` alone would go stale when the *first* box is
 * edited afterwards, leaving a form that shows no error and still refuses to submit.
 *
 * An empty second box is not a mismatch. That is an unfinished form, and the required
 * validator already has something to say about it; reporting both at once would tell the
 * user off for not having typed yet.
 */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value as string;
  const confirmation = group.get('confirmPassword')?.value as string;

  if (!confirmation) {
    return null;
  }

  return password === confirmation ? null : { passwordsDiffer: true };
}
