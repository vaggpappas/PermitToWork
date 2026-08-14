import { HttpErrorResponse } from '@angular/common/http';

/**
 * Turns whatever the API returned into one sentence a person can act on.
 *
 * The backend speaks ProblemDetails throughout, but in three shapes: a `detail` string for
 * domain and conflict errors, an `errors` dictionary for model-validation failures, and
 * nothing useful at all for a 500. This is the one place that knows the difference, so no
 * component has to.
 */
export function describeError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Something went wrong.';
  }

  if (error.status === 0) {
    return 'Cannot reach the API. Is it running on https://localhost:7188?';
  }

  const body = error.error;

  // Validation failures: { errors: { Email: ["The Email field is not a valid e-mail address."] } }
  if (body?.errors && typeof body.errors === 'object') {
    const messages = Object.values(body.errors as Record<string, string[]>).flat();
    if (messages.length > 0) {
      return messages.join(' ');
    }
  }

  if (typeof body?.detail === 'string' && body.detail.length > 0) {
    return body.detail;
  }

  if (typeof body?.title === 'string' && body.title.length > 0) {
    return body.title;
  }

  return `Request failed (${error.status}).`;
}
