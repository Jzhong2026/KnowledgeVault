import { Component, computed, input, output } from '@angular/core';

import { KnowledgeItemSummary } from '../../../../core/models/knowledge.models';

@Component({
  selector: 'app-document-tile',
  template: `
    <article
      class="tile tile--document"
      draggable="true"
      (click)="open.emit(document().id)"
      (dragstart)="onDragStart($event)"
      (dragend)="dragEnd.emit()"
    >
      <div class="tile__icon tile__icon--document" aria-hidden="true">
        <svg viewBox="0 0 24 24">
          <path d="M6 2h9l5 5v15H6z" />
          <path d="M15 2v6h5" />
          <path d="M9 13h6M9 17h4" />
        </svg>
      </div>
      <div class="tile__body">
        <h3 class="tile__title" [title]="document().title">{{ document().title }}</h3>
        <div class="tile__status" [title]="statusLabel()" aria-label="Status: {{ statusLabel() }}">
          <span class="tile__status-dot" [class]="'tile__status-dot--' + statusKey()"></span>
          <span class="tile__status-label">{{ statusLabel() }}</span>
        </div>
      </div>
      <div class="tile__actions" (click)="$event.stopPropagation()">
        <button type="button" class="tile__action" title="Download" (click)="download.emit(document().id)">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M12 4v10" />
            <path d="M8 10l4 4 4-4" />
            <path d="M5 18h14" />
          </svg>
        </button>
        @if (statusKey() === 'archived') {
          <button type="button" class="tile__action" title="Restore" (click)="restore.emit(document().id)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 18V8M8 12l4-4 4 4M5 5h14v14H5z" /></svg>
          </button>
        } @else {
          <button type="button" class="tile__action tile__action--danger" title="Archive" (click)="delete.emit(document().id)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 5h14v14H5zM8 5v-2h8v2M8 12h8" /></svg>
          </button>
        }
      </div>
    </article>
  `,
  styles: [
    `
      .tile--document {
        cursor: pointer;
      }
      .tile__icon {
        display: grid;
        width: 34px;
        height: 34px;
        flex: 0 0 auto;
        place-items: center;
        border-radius: 8px;
      }
      .tile__icon--document {
        background: #dbeafe;
        color: #1d4ed8;
      }
      .tile__icon svg {
        width: 18px;
        height: 18px;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.7;
        stroke-linecap: round;
        stroke-linejoin: round;
      }
      .tile__body {
        min-width: 0;
        flex: 1;
      }
      .tile__title {
        margin: 0;
        color: var(--text, #0f172a);
        font-size: 13px;
        font-weight: 600;
        line-height: 1.3;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .tile__status {
        display: flex;
        align-items: center;
        gap: 5px;
        margin-top: 4px;
        min-height: 14px;
        color: var(--muted, #64748b);
        font-size: 11px;
      }
      .tile__status-dot {
        display: inline-block;
        width: 7px;
        height: 7px;
        border-radius: 50%;
        background: #94a3b8;
      }
      .tile__status-dot--active {
        background: #10b981;
      }
      .tile__status-dot--draft {
        background: #f59e0b;
      }
      .tile__status-dot--archived {
        background: #94a3b8;
      }
      .tile__status-dot--review {
        background: #6366f1;
      }
      .tile__actions {
        display: flex;
        gap: 4px;
        flex: 0 0 auto;
        max-width: 0;
        overflow: hidden;
        opacity: 0;
        transition:
          max-width 140ms ease,
          opacity 120ms ease;
      }
      .tile--document:hover .tile__actions,
      .tile--document:focus-within .tile__actions {
        max-width: 80px;
        opacity: 1;
      }
      .tile__action {
        display: grid;
        width: 24px;
        height: 24px;
        place-items: center;
        border: 1px solid var(--border, #e2e8f0);
        border-radius: 6px;
        background: #ffffff;
        color: var(--text, #0f172a);
        cursor: pointer;
        padding: 0;
      }
      .tile__action:hover {
        border-color: var(--accent, #10b981);
        color: var(--accent-strong, #0f9d76);
      }
      .tile__action--danger:hover {
        border-color: #fecaca;
        color: #dc2626;
      }
      .tile__action svg {
        width: 13px;
        height: 13px;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.8;
        stroke-linecap: round;
        stroke-linejoin: round;
      }
    `,
  ],
})
export class DocumentTile {
  readonly document = input.required<KnowledgeItemSummary>();
  readonly open = output<string>();
  readonly download = output<string>();
  readonly delete = output<string>();
  readonly restore = output<string>();
  readonly dragEnd = output<void>();

  readonly statusKey = computed(() => this.document().status?.toString().toLowerCase() ?? '');
  readonly statusLabel = computed(() => {
    const raw = this.document().status?.toString() ?? '';
    if (!raw) return '';
    return raw.charAt(0).toUpperCase() + raw.slice(1).toLowerCase();
  });

  onDragStart(event: DragEvent): void {
    const id = this.document().id;
    event.dataTransfer?.setData('application/x-kv-document-id', id);
    event.dataTransfer?.setData('text/plain', id);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
  }
}
