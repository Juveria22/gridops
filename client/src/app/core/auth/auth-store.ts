import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { API_URL } from '../api/http-params';
import { CurrentUser, LoginResponse } from '../models';

interface Session {
  token: string;
  expiresAt: string;
  user: CurrentUser;
}

const STORAGE_KEY = 'gridops.session';

// localStorage so a refresh keeps you logged in.
// trade-off: readable by any script on the page (XSS). httpOnly cookie is the safer option
function readStoredSession(): Session | null {
  try {
    const session = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null') as Session | null;
    return session && new Date(session.expiresAt) > new Date() ? session : null;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly api = inject(API_URL);

  private readonly session = signal<Session | null>(readStoredSession());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isDispatcher = computed(() => this.user()?.role === 'Dispatcher');

  login(email: string, password: string) {
    return this.http.post<LoginResponse>(`${this.api}/auth/login`, { email, password }).pipe(
      tap((res) => {
        const session = { token: res.accessToken, expiresAt: res.expiresAt, user: res.user };
        localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
        this.session.set(session);
      }),
    );
  }

  logout(reason?: 'expired') {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
    this.router.navigate(['/login'], { queryParams: reason ? { reason } : {} });
  }

  // checks expiry now, not just at startup
  hasValidSession(): boolean {
    const session = this.session();
    return !!session && new Date(session.expiresAt) > new Date();
  }

  homeRoute(): string {
    return this.isDispatcher() ? '/dashboard' : '/my-work';
  }
}
