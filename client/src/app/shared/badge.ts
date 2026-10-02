import { Component, computed, input } from '@angular/core';
import { statusLabel } from '../core/models';

// colored pill for priority / status values. color picked by value in CSS
@Component({
  selector: 'app-badge',
  template: `<span class="badge" [attr.data-value]="value()">{{ label() }}</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
    [data-value='Critical'] { background: #b3261e; color: #fff; }
    [data-value='High'] { background: #ffdbcb; color: #6e2b00; }
    [data-value='Medium'] { background: #fff1c2; color: #5c4300; }
    [data-value='Reported'], [data-value='Open'] { background: #d6e3ff; color: #001b3e; }
    [data-value='Investigating'], [data-value='Assigned'] { background: #eaddff; color: #22005d; }
    [data-value='Restoring'], [data-value='InProgress'] { background: #ffe08a; color: #3d2e00; }
    [data-value='Resolved'], [data-value='Completed'] { background: #c4eed0; color: #00210f; }
  `,
})
export class Badge {
  readonly value = input.required<string>();
  protected readonly label = computed(() => statusLabel(this.value()));
}
