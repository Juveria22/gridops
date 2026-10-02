import { Injectable, signal } from '@angular/core';
import { Sort } from '@angular/material/sort';
import { ACTIVE_OUTAGE_STATUSES, Borough, OutageStatus, Priority } from '../../core/models';

export interface DashboardFilters {
  search: string;
  status: OutageStatus[];
  priority: Priority[];
  borough: Borough[];
  from: Date | null;
  to: Date | null;
}

export const DEFAULT_FILTERS: DashboardFilters = {
  search: '',
  status: ACTIVE_OUTAGE_STATUSES,
  priority: [],
  borough: [],
  from: null,
  to: null,
};

// root singleton -> filters survive going to an outage and back
@Injectable({ providedIn: 'root' })
export class DashboardState {
  readonly filters = signal<DashboardFilters>(DEFAULT_FILTERS);
  readonly sort = signal<Sort>({ active: 'reportedAt', direction: 'desc' });
  readonly pageSize = signal(25);
}
