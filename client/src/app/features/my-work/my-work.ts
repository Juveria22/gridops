import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { WorkOrderApi } from '../../core/api/work-order-api';
import { AuthStore } from '../../core/auth/auth-store';
import { boroughLabel, Priority, WorkOrder, WorkOrderStatus } from '../../core/models';
import { errorMessage, Notifier } from '../../core/notifier';
import { Badge } from '../../shared/badge';

type Tab = 'active' | 'done';

const TAB_STATUSES: Record<Tab, WorkOrderStatus[]> = {
  active: ['Assigned', 'InProgress'],
  done: ['Completed'],
};

const PRIORITY_RANK: Record<Priority, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };

// crew home. cards instead of a table - crews are mostly on phones
@Component({
  selector: 'app-my-work',
  imports: [RouterLink, DatePipe, MatButtonModule, MatButtonToggleModule, MatIconModule, MatProgressBarModule, Badge],
  templateUrl: './my-work.html',
  styleUrl: './my-work.scss',
})
export class MyWork {
  private readonly api = inject(WorkOrderApi);
  private readonly notifier = inject(Notifier);
  protected readonly auth = inject(AuthStore);

  protected readonly tab = signal<Tab>('active');
  protected readonly busyId = signal<number | null>(null);
  protected readonly boroughLabel = boroughLabel;
  protected readonly errorMessage = errorMessage;

  // API already limits crew users to their own crew - no crewId needed
  protected readonly workOrders = rxResource({
    params: () => this.tab(),
    stream: ({ params: tab }) =>
      this.api.list({ status: TAB_STATUSES[tab], pageSize: tab === 'active' ? 100 : 25, sortDir: 'desc' }),
  });

  // active: most urgent first, in-progress above not started
  protected readonly items = computed(() => {
    // value() throws while the resource is in an error state
    const items = this.workOrders.hasValue() ? this.workOrders.value().items : [];
    if (this.tab() === 'done') return items;
    return [...items].sort(
      (a, b) =>
        PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority] ||
        Number(b.status === 'InProgress') - Number(a.status === 'InProgress'),
    );
  });

  protected setStatus(workOrder: WorkOrder, status: WorkOrderStatus) {
    this.busyId.set(workOrder.id);
    this.api.updateStatus(workOrder.id, status).subscribe({
      next: () => {
        this.notifier.success(status === 'Completed' ? 'Marked completed' : 'Work started');
        this.workOrders.reload();
        this.busyId.set(null);
      },
      error: (err) => {
        this.notifier.error(err);
        this.busyId.set(null);
      },
    });
  }
}
