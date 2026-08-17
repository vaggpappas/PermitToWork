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

  // Two different ways of saying the same thing, depending on who noticed first.
  //
  // 0 is the browser: the request never got a response at all. 502 and 504 are the Angular
  // dev-server proxy: it is running, forwarded the call to https://localhost:7188, and found
  // nothing there. Reporting that as "Request failed (502)" sends people looking for a
  // problem with their password, which is the one thing it is never about.
  if (error.status === 0 || error.status === 502 || error.status === 504) {
    return 'Cannot reach the API. Is it running? Start it with: dotnet run --project src/PermitToWork.Api';
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
