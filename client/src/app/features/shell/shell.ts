import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/auth/auth-store';

// layout for logged-in pages
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule, MatIconModule],
  template: `
    <mat-toolbar class="toolbar">
      <a routerLink="/" class="brand"><mat-icon>bolt</mat-icon> GridOps</a>
      <nav>
        @if (auth.isDispatcher()) {
          <a mat-button routerLink="/dashboard" routerLinkActive="active">Outages</a>
        } @else {
          <a mat-button routerLink="/my-work" routerLinkActive="active">My work</a>
        }
      </nav>
      <span class="spacer"></span>
      <span class="user">{{ auth.user()?.displayName }} · {{ auth.user()?.crewName ?? auth.user()?.role }}</span>
      <button mat-icon-button (click)="auth.logout()" aria-label="Sign out" title="Sign out">
        <mat-icon>logout</mat-icon>
      </button>
    </mat-toolbar>
    <main class="content">
      <router-outlet />
    </main>
  `,
  styles: `
    .toolbar { gap: 8px; background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); }
    .toolbar a, .toolbar button { color: inherit; }
    .brand { display: flex; align-items: center; gap: 4px; text-decoration: none; font-weight: 500; margin-right: 16px; }
    .active { background: color-mix(in srgb, currentColor 15%, transparent); }
    .spacer { flex: 1; }
    .user { font: var(--mat-sys-body-medium); opacity: 0.9; }
    .content { padding: 24px; max-width: 1280px; margin: 0 auto; box-sizing: border-box; }
    @media (max-width: 600px) { .user { display: none; } .content { padding: 16px; } }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthStore);
}
