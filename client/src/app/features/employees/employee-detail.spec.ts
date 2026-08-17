import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { expiringIn, jwtWith } from '../../../testing/jwt';
import { Roles } from '../../core/auth/auth.service';
import { EmployeeDetail as Employee, EmployeeSummary } from '../../core/models';
import { EmployeeDetail } from './employee-detail';

const TokenStorageKey = 'ptw.accessToken';

const PersonId = '0195f2a1-0000-7000-8000-00000000000a';
const ManagerId = '0195f2a1-0000-7000-8000-00000000000b';

/**
 * Driven through the rendered component and a fake HTTP backend, so what is asserted is what
 * a user would see and what the server would receive — not which private signal holds what.
 * A rewrite of the internals that keeps the behaviour should leave these passing; that is the
 * whole point of testing at this level.
 */
describe('EmployeeDetail', () => {
  let backend: HttpTestingController;

  afterEach(() => localStorage.removeItem(TokenStorageKey));

  it('shows who the person reports to, as a link to them', async () => {
    const fixture = await open(asAdministrator(), person({ managerId: ManagerId, managerName: 'Sofia Iliadi' }));

    expect(text(fixture)).toContain('Sofia Iliadi');
    expect(html(fixture)).toContain(`/employees/${ManagerId}`);
  });

  it('offers the reporting line to an administrator', async () => {
    const fixture = await open(asAdministrator(), person());

    expect(text(fixture)).toContain('Reporting line');
  });

  it('hides the reporting line from an ordinary employee', async () => {
    const fixture = await open(asPlainEmployee(), person());

    // Not a security measure — the API refuses them anyway. It is about not showing somebody
    // a control whose only possible outcome is a 403.
    expect(text(fixture)).not.toContain('Reporting line');
  });

  it('hides it for a terminated employee', async () => {
    const fixture = await open(asAdministrator(), person({ status: 'Terminated' }));

    expect(text(fixture)).not.toContain('Reporting line');
  });

  it('lists candidates as soon as the editor opens, without the person themselves', async () => {
    const fixture = await open(asAdministrator(), person());

    click(fixture, 'Change');
    await settle(fixture);

    const search = backend.expectOne(
      (request) => request.url === '/api/employees' && request.method === 'GET',
    );

    // Only people still employed, and never more than the API will actually return.
    expect(search.request.params.get('status')).toBe('Active');

    search.flush({
      items: [
        summary(ManagerId, 'Sofia Iliadi'),
        // The server would refuse a self-assignment; the list simply never offers it.
        summary(PersonId, 'The Person Themselves'),
      ],
      page: 1,
      pageSize: 50,
      totalCount: 2,
    });
    await settle(fixture);

    expect(html(fixture)).toContain('Sofia Iliadi');
    expect(html(fixture)).not.toContain('The Person Themselves');
  });

  it('sends the chosen manager to the API', async () => {
    const fixture = await open(asAdministrator(), person());

    click(fixture, 'Change');
    await settle(fixture);
    answerSearch([summary(ManagerId, 'Sofia Iliadi')]);
    await settle(fixture);

    select(fixture, '#managerId', ManagerId);
    await settle(fixture);
    click(fixture, 'Save');
    await settle(fixture);

    const assignment = backend.expectOne(`/api/employees/${PersonId}/manager`);
    expect(assignment.request.method).toBe('PUT');
    expect(assignment.request.body).toEqual({ managerId: ManagerId });
  });

  it('clears the reporting line by sending null, not by omitting it', async () => {
    const fixture = await open(asAdministrator(), person({ managerId: ManagerId, managerName: 'Sofia Iliadi' }));

    click(fixture, 'Change');
    await settle(fixture);
    answerSearch([]);
    await settle(fixture);

    click(fixture, 'Clear the reporting line');
    await settle(fixture);

    const assignment = backend.expectOne(`/api/employees/${PersonId}/manager`);

    // null is the instruction. An absent property would be a different request meaning
    // "no change", and the reporting line would silently stay put.
    expect(assignment.request.body).toEqual({ managerId: null });
  });

  it('shows the refusal the server gave, rather than a generic failure', async () => {
    const fixture = await open(asAdministrator(), person());

    click(fixture, 'Change');
    await settle(fixture);
    answerSearch([summary(ManagerId, 'Sofia Iliadi')]);
    await settle(fixture);

    select(fixture, '#managerId', ManagerId);
    await settle(fixture);
    click(fixture, 'Save');
    await settle(fixture);

    backend.expectOne(`/api/employees/${PersonId}/manager`).flush(
      { detail: 'An employee cannot report to themselves.' },
      { status: 422, statusText: 'Unprocessable Content' },
    );
    await settle(fixture);

    expect(text(fixture)).toContain('An employee cannot report to themselves.');
  });

  // ---------------------------------------------------------------- setting the scene

  /** Signs the user in, builds the component, and answers the four requests it makes on load. */
  async function open(token: string, detail: Employee): Promise<ComponentFixture<EmployeeDetail>> {
    localStorage.setItem(TokenStorageKey, token);

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    backend = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(EmployeeDetail);
    fixture.componentRef.setInput('id', PersonId);
    await settle(fixture);

    // The constructor asks for the lookups; load() runs in a microtask afterwards, which is
    // what lets it read the required input without racing the router.
    backend.expectOne('/api/lookups/trades').flush([]);
    backend.expectOne('/api/lookups/certification-types').flush([]);
    backend.expectOne(`/api/employees/${PersonId}`).flush(detail);
    backend.expectOne(`/api/employees/${PersonId}/teams`).flush([]);

    await settle(fixture);
    return fixture;
  }

  function answerSearch(items: EmployeeSummary[]): void {
    backend
      .expectOne((request) => request.url === '/api/employees' && request.method === 'GET')
      .flush({ items, page: 1, pageSize: 50, totalCount: items.length });
  }

  async function settle(fixture: ComponentFixture<EmployeeDetail>): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function click(fixture: ComponentFixture<EmployeeDetail>, label: string): void {
    const buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    );
    const button = buttons.find((candidate) => candidate.textContent?.trim() === label);

    if (!button) {
      throw new Error(`No button labelled "${label}". Present: ${buttons.map((b) => b.textContent?.trim()).join(', ')}`);
    }

    button.click();
  }

  function select(fixture: ComponentFixture<EmployeeDetail>, selector: string, value: string): void {
    const element = (fixture.nativeElement as HTMLElement).querySelector<HTMLSelectElement>(selector);

    if (!element) {
      throw new Error(`No element matching ${selector}.`);
    }

    element.value = value;
    element.dispatchEvent(new Event('change'));
  }

  function html(fixture: ComponentFixture<EmployeeDetail>): string {
    return (fixture.nativeElement as HTMLElement).innerHTML;
  }

  function text(fixture: ComponentFixture<EmployeeDetail>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function person(overrides: Partial<Employee> = {}): Employee {
    return {
      id: PersonId,
      employeeNumber: 'ACME-0001',
      firstName: 'Marta',
      lastName: 'Nowak',
      fullName: 'Marta Nowak',
      email: 'marta.nowak@example.test',
      phoneNumber: null,
      address: null,
      dateOfBirth: null,
      age: null,
      jobTitle: 'Pipe Fitter',
      tradeId: '0195f2a1-0000-7000-8000-000000000001',
      tradeName: 'Pipe Fitter',
      companyId: '0195f2a1-0000-7000-8000-000000000002',
      companyName: 'Acme Maintenance Services',
      managerId: null,
      managerName: null,
      hireDate: '2021-04-12',
      status: 'Active',
      accessRole: 'Employee',
      hasAccount: true,
      certifications: [],
      ...overrides,
    };
  }

  function summary(id: string, fullName: string): EmployeeSummary {
    const [firstName, ...rest] = fullName.split(' ');

    return {
      id,
      employeeNumber: 'ACME-0002',
      firstName,
      lastName: rest.join(' '),
      fullName,
      email: 'someone@example.test',
      jobTitle: 'Supervisor',
      tradeName: 'Supervisor',
      companyName: 'Acme Maintenance Services',
      status: 'Active',
      accessRole: 'Supervisor',
      hasAccount: true,
    };
  }

  function asAdministrator(): string {
    return jwtWith({ role: Roles.Administrator, 'ptw:scope': 'all', exp: expiringIn(60) });
  }

  function asPlainEmployee(): string {
    return jwtWith({ role: Roles.Employee, exp: expiringIn(60) });
  }
});
