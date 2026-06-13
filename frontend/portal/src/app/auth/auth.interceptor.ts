import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

// Attaches the bearer token to same-API requests. Skips the auth endpoints
// themselves (login/refresh carry no token) and any non-API URL.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();

  const isApi = req.url.startsWith(environment.apiBase) || req.url.includes('/api/v1/');
  const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/refresh');

  if (!token || !isApi || isAuthEndpoint) {
    return next(req);
  }

  return next(
    req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }),
  );
};
