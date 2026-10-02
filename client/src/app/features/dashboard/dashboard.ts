import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import {
  BehaviorSubject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  startWith,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import { OutageApi } from '../../core/api/outage-api';
import {
  BOROUGHS,
  boroughLabel,
  OUTAGE_STATUSES,
  OutageQuery,
  OutageSortField,
  OutageSummary,
  PagedResult,
  PRIORITIES,
} from '../../core/models';
import { Notifier } from '../../core/notifier';
import { Badge } from '../../shared/badge';
import { DashboardFilters, DashboardState, DEFAULT_FILTERS } from './dashboard-state';
import { NewOutageDialog } from './new-outage-dialog';

const EMPTY_PAGE: PagedResult<OutageSummary> = { items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 };

@Component({
  selector: 'app-dashboard',
  imports: [
    ReactiveFormsModule,
    DatePipe,
    DecimalPipe,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    Badge,
  ],
  providers: [provideNativeDateAdapter()],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly api = inject(OutageApi);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  protected readonly state = inject(DashboardState);

  protected readonly boroughs = BOROUGHS;
  protected readonly priorities = PRIORITIES;
  protected readonly statuses = OUTAGE_STATUSES;
  protected readonly boroughLabel = boroughLabel;
  protected readonly columns = ['priority', 'title', 'status', 'customersAffected', 'openWorkOrders', 'reportedAt'];

  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly saved = this.state.filters();

  // arrays wrapped in fb.control - a bare array means [value, validators] to FormBuilder
  protected readonly form = this.fb.group({
    search: this.saved.search,
    status: this.fb.control(this.saved.status),
    priority: this.fb.control(this.saved.priority),
    borough: this.fb.control(this.saved.borough),
    from: this.fb.control<Date | null>(this.saved.from),
    to: this.fb.control<Date | null>(this.saved.to),
  });

  private readonly sort$ = new BehaviorSubject<Sort>(this.state.sort());
  private readonly page$ = new Subject<PageEvent>();

  protected readonly loading = signal(true);

  // typing/clicking filters -> wait 300ms of quiet -> skip if nothing actually changed
  private readonly filters$ = this.form.valueChanges.pipe(
    debounceTime(300),
    startWith(null), // first load right away, no debounce
    map(() => this.form.getRawValue()),
    distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
    tap((filters) => this.state.filters.set(filters)),
  );

  // new filters or sort -> restart paging at page 1. paging alone keeps filters
  private readonly query$ = combineLatest([this.filters$, this.sort$]).pipe(
    switchMap(([filters, sort]) =>
      this.page$.pipe(
        startWith({ pageIndex: 0, pageSize: this.state.pageSize() }),
        map((page) => toQuery(filters, sort, page.pageIndex, page.pageSize)),
      ),
    ),
  );

  protected readonly result = toSignal(
    this.query$.pipe(
      tap(() => this.loading.set(true)),
      // switchMap cancels the previous request -> a slow old response can't overwrite newer results
      switchMap((query) =>
        this.api.list(query).pipe(
          catchError((err) => {
            this.notifier.error(err);
            return of(EMPTY_PAGE);
          }),
        ),
      ),
      tap(() => this.loading.set(false)),
    ),
    { initialValue: EMPTY_PAGE },
  );

  protected onSort(sort: Sort) {
    this.state.sort.set(sort);
    this.sort$.next(sort);
  }

  protected onPage(event: PageEvent) {
    this.state.pageSize.set(event.pageSize);
    this.page$.next(event);
  }

  protected clearFilters() {
    this.form.reset(DEFAULT_FILTERS);
  }

  protected newOutage() {
    this.dialog
      .open(NewOutageDialog, { width: '560px' })
      .afterClosed()
      .subscribe((created) => {
        if (!created) return;
        this.notifier.success('Outage created');
        this.router.navigate(['/outages', created.id]);
      });
  }

  protected open(outage: OutageSummary) {
    this.router.navigate(['/outages', outage.id]);
  }
}

function toQuery(f: DashboardFilters, sort: Sort, pageIndex: number, pageSize: number): OutageQuery {
  return {
    search: f.search.trim() || undefined,
    status: f.status,
    priority: f.priority,
    borough: f.borough,
    from: f.from ? startOfDay(f.from).toISOString() : undefined,
    to: f.to ? endOfDay(f.to).toISOString() : undefined,
    sortBy: sort.active as OutageSortField,
    sortDir: sort.direction || 'desc',
    page: pageIndex + 1, // paginator is 0-based, API is 1-based
    pageSize,
  };
}

const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
const endOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59, 999);
