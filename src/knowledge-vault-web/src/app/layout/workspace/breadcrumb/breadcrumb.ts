import { Component, input, output } from '@angular/core';

import { BreadcrumbNode } from '../../../core/workspace/workspace.service';

@Component({
  selector: 'app-breadcrumb',
  template: `
    <nav class="breadcrumb" aria-label="Folder path">
      @for (node of path(); track node.id; let last = $last) {
        <button
          type="button"
          class="breadcrumb__item"
          [class.is-current]="last"
          [disabled]="last"
          (click)="navigate.emit(node.id)"
        >
          {{ node.name }}
        </button>
        @if (!last) {
          <span class="breadcrumb__sep" aria-hidden="true">/</span>
        }
      }
    </nav>
  `,
  styles: [
    `
      .breadcrumb {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: 4px;
        min-width: 0;
      }
      .breadcrumb__item {
        border: 0;
        background: transparent;
        color: var(--accent-strong, #0f9d76);
        cursor: pointer;
        font: inherit;
        font-size: 13px;
        font-weight: 700;
        padding: 2px 4px;
        border-radius: 6px;
      }
      .breadcrumb__item:hover:not(:disabled) {
        background: #e7f6ef;
      }
      .breadcrumb__item.is-current {
        color: var(--text, #0f172a);
        cursor: default;
        font-weight: 800;
      }
      .breadcrumb__sep {
        color: var(--muted, #64748b);
        font-size: 13px;
      }
    `,
  ],
})
export class Breadcrumb {
  readonly path = input.required<BreadcrumbNode[]>();
  readonly navigate = output<string>();
}
