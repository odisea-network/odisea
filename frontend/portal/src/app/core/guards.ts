import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

function roleGuard(userType: 'agency' | 'operator'): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (auth.isAuthenticated() && auth.user()?.userType === userType) {
      return true;
    }
    return router.createUrlTree(['/login']);
  };
}

export const agencyGuard: CanActivateFn = roleGuard('agency');
export const adminGuard: CanActivateFn = roleGuard('operator');
