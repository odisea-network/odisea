import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './api.types';

/** One place to turn an HTTP failure into a user-facing message. */
export function problemMessage(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse) {
    const problem = err.error as ProblemDetails | null;
    return problem?.detail ?? problem?.title ?? fallback;
  }
  return fallback;
}
