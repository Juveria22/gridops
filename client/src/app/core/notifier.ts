import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProblemDetails } from './models';

// API errors are all ProblemDetails, so one function covers every screen
export function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Cannot reach the server.';
    const problem = err.error as ProblemDetails | null;
    if (problem?.errors) return Object.values(problem.errors).flat().join(' ');
    return problem?.detail ?? problem?.title ?? `Request failed (${err.status}).`;
  }
  return 'Something went wrong.';
}

@Injectable({ providedIn: 'root' })
export class Notifier {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string) {
    this.snackBar.open(message, undefined, { duration: 3000 });
  }

  error(err: unknown) {
    this.snackBar.open(errorMessage(err), 'Dismiss', { duration: 6000 });
  }
}
