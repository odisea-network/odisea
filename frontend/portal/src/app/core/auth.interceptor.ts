import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { from, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(withToken(request, auth.token)).pipe(
    catchError((error: unknown) => {
      const isAuthEndpoint = request.url.includes('/api/v1/auth/');
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }

      // Access token expired (they live 15 minutes): one silent refresh, one retry.
      return from(auth.refreshOnce()).pipe(
        switchMap(token => {
          if (token === null) {
            auth.logout();
            void router.navigate(['/login']);
            return throwError(() => error);
          }
          return next(withToken(request, token));
        }),
      );
    }),
  );
};

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;
}
