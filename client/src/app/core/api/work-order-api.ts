import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CreateWorkOrderRequest, PagedResult, WorkOrder, WorkOrderQuery, WorkOrderStatus } from '../models';
import { API_URL, toParams } from './http-params';

@Injectable({ providedIn: 'root' })
export class WorkOrderApi {
  private readonly http = inject(HttpClient);
  private readonly api = inject(API_URL);
  private readonly url = `${this.api}/work-orders`;

  list(query: WorkOrderQuery) {
    return this.http.get<PagedResult<WorkOrder>>(this.url, { params: toParams(query) });
  }

  get(id: number) {
    return this.http.get<WorkOrder>(`${this.url}/${id}`);
  }

  create(outageId: number, request: CreateWorkOrderRequest) {
    return this.http.post<WorkOrder>(`${this.api}/outages/${outageId}/work-orders`, request);
  }

  // null = unassign
  assignCrew(id: number, crewId: number | null) {
    return this.http.put<void>(`${this.url}/${id}/crew`, { crewId });
  }

  updateStatus(id: number, status: WorkOrderStatus) {
    return this.http.patch<void>(`${this.url}/${id}/status`, { status });
  }
}
