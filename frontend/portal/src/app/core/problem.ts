import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './api.types';
import { MessageKey, t, tOptional } from './i18n/i18n';

/** One place to turn an HTTP failure into a user-facing message. */
export function problemMessage(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse) {
    const problem = err.error as ProblemDetails | null;
    return problem?.detail ?? problem?.title ?? fallback;
  }
  return fallback;
}

/** The stable machine code some endpoints attach to a rejection (`code` extension), if any. */
export function problemCode(err: unknown): string | null {
  if (!(err instanceof HttpErrorResponse)) return null;
  const problem = err.error as ProblemDetails | null;
  if (typeof problem?.code === 'string') return problem.code;
  if (problem?.type?.endsWith('/feature-not-in-plan')) return 'feature_not_in_plan';
  if (problem?.type?.endsWith('/agency-suspended')) return 'agency_suspended';
  return null;
}

/**
 * The portal's own wording for a coded rejection. The API's English detail is never shown:
 * an unknown code falls back to the given message.
 */
export function localizedProblem(err: unknown, fallback: MessageKey = 'errors.generic'): string {
  const code = problemCode(err);
  if (code) {
    const known = tOptional(`errors.${code}`) ?? (code.endsWith('_too_long') ? t('errors.too_long') : null);
    if (known) return known;
  }
  return t(fallback);
}
