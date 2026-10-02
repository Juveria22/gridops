import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Observable, of } from 'rxjs';
import { CrewApi } from '../../core/api/crew-api';
import { WorkOrderApi } from '../../core/api/work-order-api';
import { AuthStore } from '../../core/auth/auth-store';
import { boroughLabel, Crew, WorkOrderStatus } from '../../core/models';
import { errorMessage, Notifier } from '../../core/notifier';
import { Badge } from '../../shared/badge';

@Component({
  selector: 'app-work-order-detail',
  imports: [
    FormsModule,
    RouterLink,
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressBarModule,
    Badge,
  ],
  templateUrl: './work-order-detail.html',
  styleUrl: './work-order-detail.scss',
})
export class WorkOrderDetailPage {
  private readonly api = inject(WorkOrderApi);
  private readonly crewApi = inject(CrewApi);
  private readonly notifier = inject(Notifier);
  protected readonly auth = inject(AuthStore);

  readonly id = input.required({ transform: numberAttribute });

  protected readonly workOrder = rxResource({
    params: () => this.id(),
    stream: ({ params: id }) => this.api.get(id),
  });

  // crew members can't call /crews (403), so only dispatchers load it
  protected readonly crews = rxResource<Crew[], boolean>({
    params: () => this.auth.isDispatcher(),
    stream: ({ params: isDispatcher }): Observable<Crew[]> => (isDispatcher ? this.crewApi.list() : of([])),
  });

  protected readonly saving = signal(false);
  protected readonly boroughLabel = boroughLabel;
  protected readonly errorMessage = errorMessage;

  protected readonly isFinished = computed(() => {
    if (!this.workOrder.hasValue()) return false; // value() throws in error state
    const status = this.workOrder.value().status;
    return status === 'Completed' || status === 'Cancelled';
  });

  protected readonly backLink = computed(() =>
    this.auth.isDispatcher() && this.workOrder.hasValue() ? ['/outages', this.workOrder.value().outageId] : ['/my-work'],
  );

  protected setStatus(status: WorkOrderStatus, message: string) {
    this.run(this.api.updateStatus(this.id(), status), message);
  }

  protected assignCrew(crewId: number | null) {
    this.run(this.api.assignCrew(this.id(), crewId), crewId ? 'Crew assigned' : 'Crew unassigned');
  }

  private run(request: Observable<void>, message: string) {
    this.saving.set(true);
    request.subscribe({
      next: () => {
        this.notifier.success(message);
        this.workOrder.reload();
        this.saving.set(false);
      },
      error: (err) => {
        this.notifier.error(err);
        this.workOrder.reload(); // e.g. the select showed a crew the API rejected
        this.saving.set(false);
      },
    });
  }
}
