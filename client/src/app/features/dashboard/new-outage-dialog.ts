import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { OutageApi } from '../../core/api/outage-api';
import { Borough, BOROUGHS, boroughLabel, OutageDetail, Priority, PRIORITIES } from '../../core/models';
import { errorMessage } from '../../core/notifier';

// closes with the created outage, or undefined if cancelled
@Component({
  selector: 'app-new-outage-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>New outage</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" placeholder="e.g. Transformer failure - Astoria" cdkFocusInitial />
          <mat-error>Title is required</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Borough</mat-label>
          <mat-select formControlName="borough">
            @for (b of boroughs; track b) {
              <mat-option [value]="b">{{ boroughLabel(b) }}</mat-option>
            }
          </mat-select>
          <mat-error>Borough is required</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Neighborhood</mat-label>
          <input matInput formControlName="neighborhood" />
          <mat-error>Neighborhood is required</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Priority</mat-label>
          <mat-select formControlName="priority">
            @for (p of priorities; track p) {
              <mat-option [value]="p">{{ p }}</mat-option>
            }
          </mat-select>
          <mat-error>Priority is required</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Customers affected</mat-label>
          <input matInput type="number" min="0" formControlName="customersAffected" />
          <mat-error>0 or more</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
        </mat-form-field>
        @if (error()) {
          <p class="error full" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Create</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: grid; grid-template-columns: 1fr 1fr; gap: 0 12px; }
    .full { grid-column: 1 / -1; }
    .error { color: var(--mat-sys-error); margin: 0; }
    @media (max-width: 600px) { .fields { grid-template-columns: 1fr; } }
  `,
})
export class NewOutageDialog {
  private readonly api = inject(OutageApi);
  private readonly dialogRef = inject(MatDialogRef<NewOutageDialog, OutageDetail>);

  protected readonly boroughs = BOROUGHS;
  protected readonly priorities = PRIORITIES;
  protected readonly boroughLabel = boroughLabel;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  // same limits as the API's [MaxLength]/[Range] - API still validates
  protected readonly form = inject(FormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    borough: [null as Borough | null, Validators.required],
    neighborhood: ['', [Validators.required, Validators.maxLength(100)]],
    priority: [null as Priority | null, Validators.required],
    customersAffected: [0, [Validators.required, Validators.min(0), Validators.max(10_000_000)]],
    description: ['', Validators.maxLength(2000)],
  });

  protected submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .create({
        title: v.title!,
        borough: v.borough!,
        neighborhood: v.neighborhood!,
        priority: v.priority!,
        customersAffected: v.customersAffected ?? 0,
        description: v.description || null,
      })
      .subscribe({
        next: (outage) => this.dialogRef.close(outage),
        error: (err) => {
          this.error.set(errorMessage(err));
          this.saving.set(false);
        },
      });
  }
}
