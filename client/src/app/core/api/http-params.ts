import { HttpParams } from '@angular/common/http';
import { InjectionToken } from '@angular/core';

// '/api' -> dev proxy locally. phase 7 points this at the deployed API
export const API_URL = new InjectionToken<string>('API_URL', { factory: () => '/api' });

// arrays -> repeated keys (?status=A&status=B), which is what ASP.NET binds to arrays
export function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) value.forEach((v) => (params = params.append(key, String(v))));
    else params = params.set(key, String(value));
  }
  return params;
}
