import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  CreateOutageRequest,
  OutageDetail,
  OutageQuery,
  OutageStatus,
  OutageSummary,
  PagedResult,
} from '../models';
import { API_URL, toParams } from './http-params';

@Injectable({ providedIn: 'root' })
export class OutageApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_URL)}/outages`;

  list(query: OutageQuery) {
    return this.http.get<PagedResult<OutageSummary>>(this.url, { params: toParams(query) });
  }

  get(id: number) {
    return this.http.get<OutageDetail>(`${this.url}/${id}`);
  }

  create(request: CreateOutageRequest) {
    return this.http.post<OutageDetail>(this.url, request);
  }

  updateStatus(id: number, status: OutageStatus) {
    return this.http.patch<void>(`${this.url}/${id}/status`, { status });
  }
}
