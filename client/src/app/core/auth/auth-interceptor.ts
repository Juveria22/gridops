import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { API_URL } from '../api/http-params';
import { AuthStore } from './auth-store';

// runs on every request. like .NET middleware but client side
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const token = auth.token();

  // only our API gets the token - never leak it to third-party URLs
  const isApiCall = req.url.startsWith(inject(API_URL));
  const request = token && isApiCall ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((err: HttpErrorResponse) => {
      // expired/invalid token -> back to login. a failed login is also 401 but handled by the form
      if (err.status === 401 && isApiCall && !req.url.endsWith('/auth/login')) auth.logout('expired');
      return throwError(() => err);
    }),
  );
};
