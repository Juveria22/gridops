import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { AuthStore } from './core/auth/auth-store';
import { authGuard, guestGuard, roleGuard } from './core/auth/guards';

// loadComponent = lazy loaded. each page downloads the first time it's opened
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/shell/shell').then((m) => m.Shell),
    children: [
      {
        path: 'dashboard',
        canActivate: [roleGuard('Dispatcher')],
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'outages/:id',
        canActivate: [roleGuard('Dispatcher')],
        loadComponent: () => import('./features/outage-detail/outage-detail').then((m) => m.OutageDetailPage),
      },
      {
        // both roles. API only returns crew members their own
        path: 'work-orders/:id',
        loadComponent: () =>
          import('./features/work-order-detail/work-order-detail').then((m) => m.WorkOrderDetailPage),
      },
      {
        path: 'my-work',
        canActivate: [roleGuard('Crew')],
        loadComponent: () => import('./features/my-work/my-work').then((m) => m.MyWork),
      },
      {
        // "/" -> home page for your role
        path: '',
        pathMatch: 'full',
        canActivate: [() => inject(Router).createUrlTree([inject(AuthStore).homeRoute()])],
        children: [],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
