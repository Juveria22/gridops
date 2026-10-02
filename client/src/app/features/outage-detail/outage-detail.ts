import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { CrewApi } from '../../core/api/crew-api';
import { OutageApi } from '../../core/api/outage-api';
import { WorkOrderApi } from '../../core/api/work-order-api';
import { boroughLabel, OutageStatus, WorkOrder } from '../../core/models';
import { errorMessage, Notifier } from '../../core/notifier';
import { Badge } from '../../shared/badge';

@Component({
  selector: 'app-outage-detail',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatProgressBarModule,
    Badge,
  ],
  templateUrl: './outage-detail.html',
  styleUrl: './outage-detail.scss',
})
export class OutageDetailPage {
  private readonly outages = inject(OutageApi);
  private readonly workOrders = inject(WorkOrderApi);
  private readonly crewApi = inject(CrewApi);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);

  // :id from the route (withComponentInputBinding)
  readonly id = input.required({ transform: numberAttribute });

  // refetches whenever id() changes. value/isLoading/error are signals
  protected readonly outage = rxResource({
    params: () => this.id(),
    stream: ({ params: id }) => this.outages.get(id),
  });

  protected readonly crews = rxResource({ stream: () => this.crewApi.list() });

  // crews from the outage's borough first, least busy first
  protected readonly sortedCrews = computed(() => {
    // value() throws in error state -> check hasValue first
    const borough = this.outage.hasValue() ? this.outage.value().borough : undefined;
    const crews = this.crews.hasValue() ? this.crews.value() : [];
    return [...crews].sort(
      (a, b) =>
        Number(b.homeBorough === borough) - Number(a.homeBorough === borough) ||
        a.openWorkOrders - b.openWorkOrders,
    );
  });

  protected readonly isResolved = computed(() => this.outage.hasValue() && this.outage.value().status === 'Resolved');
  protected readonly saving = signal(false);
  protected readonly boroughLabel = boroughLabel;
  protected readonly errorMessage = errorMessage;
  protected readonly workOrderColumns = ['title', 'crew', 'status', 'priority'];
  protected readonly progressStatuses: OutageStatus[] = ['Reported', 'Investigating', 'Restoring'];

  protected readonly form = inject(FormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    crewId: [null as number | null],
    notes: ['', Validators.maxLength(2000)],
  });

  protected setStatus(status: OutageStatus) {
    this.saving.set(true);
    this.outages.updateStatus(this.id(), status).subscribe({
      next: () => {
        this.notifier.success(status === 'Resolved' ? 'Outage resolved' : `Status set to ${status}`);
        this.outage.reload();
        this.saving.set(false);
      },
      error: (err) => {
        this.notifier.error(err); // e.g. 409 "has open work orders"
        this.saving.set(false);
      },
    });
  }

  protected addWorkOrder() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { title, crewId, notes } = this.form.getRawValue();
    this.saving.set(true);
    this.workOrders.create(this.id(), { title: title!, crewId, notes: notes || null }).subscribe({
      next: () => {
        this.notifier.success('Work order added');
        this.form.reset();
        this.outage.reload();
        this.crews.reload(); // workload counts changed
        this.saving.set(false);
      },
      error: (err) => {
        this.notifier.error(err);
        this.saving.set(false);
      },
    });
  }

  protected openWorkOrder(workOrder: WorkOrder) {
    this.router.navigate(['/work-orders', workOrder.id]);
  }
}
