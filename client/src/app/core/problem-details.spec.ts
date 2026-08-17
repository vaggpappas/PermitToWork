import { HttpErrorResponse } from '@angular/common/http';
import { describeError } from './problem-details';

/**
 * The last link in a chain that starts in the domain model.
 *
 * `Permit.AddWorker` throws a sentence written for a human. `ApiExceptionHandler` turns it
 * into a 422 with that sentence in `detail`. The integration test asserts it survives the
 * exception handler. This asserts it survives the last step, onto the screen — because a
 * carefully worded refusal that the UI renders as "Request failed (422)" was wasted effort.
 */
describe('describeError', () => {
  it('shows the reason a domain rule gave', () => {
    const message =
      'Marta Nowak does not hold a valid Hot Work certificate for the whole permit period.';

    expect(describeError(problem(422, { detail: message }))).toBe(message);
  });

  it('flattens a validation dictionary into something readable', () => {
    const result = describeError(
      problem(400, {
        errors: {
          Email: ['The Email field is not a valid e-mail address.'],
          JobTitle: ['The JobTitle field is required.'],
        },
      }),
    );

    expect(result).toContain('not a valid e-mail address');
    expect(result).toContain('JobTitle field is required');
  });

  it('prefers the validation detail over the generic title', () => {
    const result = describeError(
      problem(400, {
        title: 'One or more validation errors occurred.',
        errors: { Email: ['The Email field is required.'] },
      }),
    );

    // "One or more validation errors occurred" tells the user nothing they can act on. The
    // field-level message tells them which box to go and fix.
    expect(result).toBe('The Email field is required.');
  });

  it('falls back to the title when there is no detail', () => {
    expect(describeError(problem(409, { title: 'That email is already registered.' })))
      .toBe('That email is already registered.');
  });

  it('names the status when the body says nothing useful', () => {
    // A 500 deliberately carries no detail — the server does not leak its internals. The
    // user still deserves better than a blank alert box.
    expect(describeError(problem(500, {}))).toBe('Request failed (500).');
  });

  it('explains an unreachable API instead of showing a zero', () => {
    // Status 0 is the browser saying the request never got a response at all.
    expect(describeError(problem(0, null))).toContain('Cannot reach the API');
  });

  it('treats a dead proxy target the same as an unreachable API', () => {
    // 502 is the Angular dev server saying it forwarded the call and found nothing behind
    // it. Same cause, different messenger. Shown as "Request failed (502)" it reads like a
    // rejected login, which sends people to check a password that was never the problem.
    expect(describeError(problem(502, null))).toContain('Cannot reach the API');
    expect(describeError(problem(504, null))).toContain('Cannot reach the API');
  });

  it('does not render an arbitrary thrown value as [object Object]', () => {
    expect(describeError(new Error('boom'))).toBe('Something went wrong.');
    expect(describeError({ some: 'object' })).toBe('Something went wrong.');
    expect(describeError(undefined)).toBe('Something went wrong.');
  });

  it('ignores an errors dictionary that is empty', () => {
    // ProblemDetails allows `errors: {}`. Taking that branch would join nothing into an
    // empty string and show the user a blank alert.
    expect(describeError(problem(400, { errors: {}, detail: 'Nothing was sent.' })))
      .toBe('Nothing was sent.');
  });

  function problem(status: number, body: unknown): HttpErrorResponse {
    return new HttpErrorResponse({ status, error: body, url: '/api/permits' });
  }
});
