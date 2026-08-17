import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { expiringIn, jwtWith } from '../../../testing/jwt';
import { Roles } from '../../core/auth/auth.service';
import { Login } from './login';

/**
 * The registration form.
 *
 * The confirmation box exists to catch a typed-once-wrong password — a mistake that is
 * otherwise discovered days later, by somebody who cannot sign in and does not know why.
 */
describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let backend: HttpTestingController;

  beforeEach(async () => {
    localStorage.removeItem('ptw.accessToken');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'employees', children: [] }]),
      ],
    });

    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Login);
    await settle();
  });

  afterEach(() => localStorage.removeItem('ptw.accessToken'));

  it('does not ask for a confirmation when signing in', async () => {
    expect(field('#confirmPassword')).toBeNull();

    // And the form must still be submittable. A confirmation control left required while
    // hidden gives a button that does nothing and no explanation on screen.
    type('#email', 'admin@permittowork.local');
    type('#password', 'Admin!23456');
    await settle();

    submit();
    await settle();

    backend.expectOne('/api/auth/login');
  });

  it('asks for the password twice when registering', async () => {
    await switchToRegister();

    expect(field('#confirmPassword')).not.toBeNull();
    expect(text()).toContain('Re-type your password');

    // The same claim the WelcomeEmail tests pin on the server side: nobody issued them a
    // password. Said on the screen as well as in the invitation, because whichever one they
    // read first is the one that has to be right.
    expect(text()).toContain('nobody has set one for you');
  });

  it('says so when the two passwords differ', async () => {
    await switchToRegister();

    type('#email', 'marta@acme.test');
    type('#password', 'Str0ng!Passw0rd');
    type('#confirmPassword', 'Str0ng!Passw0rdd');
    await settle();

    expect(text()).toContain('The passwords do not match');
  });

  it('will not send a registration while they differ', async () => {
    await switchToRegister();

    type('#email', 'marta@acme.test');
    type('#password', 'Str0ng!Passw0rd');
    type('#confirmPassword', 'something-else-entirely');
    await settle();

    submit();
    await settle();

    // Nothing left the browser. Letting it through would create an account with a password
    // the user does not think they chose.
    backend.expectNone('/api/auth/register');
  });

  it('notices when the first box is corrected afterwards', async () => {
    await switchToRegister();

    type('#password', 'Str0ng!Passw0rd');
    type('#confirmPassword', 'Str0ng!Passw0rdd');
    await settle();
    expect(text()).toContain('The passwords do not match');

    // Fixing the *first* field has to clear the error too. A validator attached to the
    // confirmation alone would not re-run here, leaving a form that shows no error and still
    // refuses to submit — which is why the check sits on the group.
    type('#password', 'Str0ng!Passw0rdd');
    await settle();

    expect(text()).not.toContain('The passwords do not match');
  });

  it('registers once they match, and keeps the confirmation in the browser', async () => {
    await switchToRegister();

    type('#email', 'marta@acme.test');
    type('#password', 'Str0ng!Passw0rd');
    type('#confirmPassword', 'Str0ng!Passw0rd');
    await settle();

    submit();
    await settle();

    const request = backend.expectOne('/api/auth/register');

    // The API takes an email and a password. The confirmation is a browser-side check, and
    // sending it would invite a server that eventually starts trusting it.
    expect(request.request.body).toEqual({ email: 'marta@acme.test', password: 'Str0ng!Passw0rd' });

    request.flush({
      accessToken: jwtWith({ role: Roles.Employee, exp: expiringIn(60) }),
      expiresAtUtc: new Date().toISOString(),
    });
  });

  it('reveals both boxes at once', async () => {
    await switchToRegister();

    expect(field('#password')?.type).toBe('password');
    expect(field('#confirmPassword')?.type).toBe('password');

    click('Show password');
    await settle();

    // One control for both, because comparing them is the entire point of the second box.
    expect(field('#password')?.type).toBe('text');
    expect(field('#confirmPassword')?.type).toBe('text');
  });

  async function switchToRegister(): Promise<void> {
    click('Register');
    await settle();
  }

  function field(selector: string): HTMLInputElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(selector);
  }

  function type(selector: string, value: string): void {
    const input = field(selector);
    if (!input) {
      throw new Error(`No input matching ${selector}.`);
    }

    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
  }

  function click(label: string): void {
    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    const button = buttons.find(
      (candidate) =>
        candidate.textContent?.trim() === label || candidate.getAttribute('aria-label') === label,
    );

    if (!button) {
      throw new Error(`No button labelled "${label}".`);
    }

    button.click();
  }

  function submit(): void {
    (fixture.nativeElement as HTMLElement).querySelector('form')?.dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }
});
