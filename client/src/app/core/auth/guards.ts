import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserRole } from '../models';
import { AuthStore } from './auth-store';

// UX only - hides pages you can't use. real security is the API's 401/403

export const authGuard: CanActivateFn = (_, state) => {
  const auth = inject(AuthStore);
  return auth.hasValidSession()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

// wrong role -> your own home page instead of an error
export const roleGuard =
  (role: UserRole): CanActivateFn =>
  () => {
    const auth = inject(AuthStore);
    const router = inject(Router);
    if (!auth.hasValidSession()) return router.createUrlTree(['/login']);
    return auth.user()?.role === role ? true : router.createUrlTree([auth.homeRoute()]);
  };

// already logged in -> skip the login page
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  return auth.hasValidSession() ? inject(Router).createUrlTree([auth.homeRoute()]) : true;
};
