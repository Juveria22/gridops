import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Borough, Crew } from '../models';
import { API_URL, toParams } from './http-params';

@Injectable({ providedIn: 'root' })
export class CrewApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_URL)}/crews`;

  list(borough?: Borough) {
    return this.http.get<Crew[]>(this.url, { params: toParams({ borough }) });
  }
}
