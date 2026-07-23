import { Component, input, output } from '@angular/core';

import { KnowledgeItemSummary } from '../../../../core/models/knowledge.models';
import { StatusPill } from '../../../../shared/components/status-pill/status-pill';

@Component({
  selector: 'app-document-tile',
  imports: [StatusPill],
  template: `
    <article class="tile tile--document" (click)="open.emit(document().id)">
      <div class="tile__icon" aria-hidden="true">
        <svg viewBox="0 0 24 24"><path d="M6 3h9l4 4v14H6zM15 3v5h4" /></svg>
      </div>
      <div class="tile__body">
        <h3 class="tile__title" [title]="document().title">{{ document().title }}</h3>
        @if (document().summary) {
          <p class="tile__summary">{{ document().summary }}</p>
        }
        <div class="tile__footer">
          <app-status-pill [status]="document().status" />
        </div>
      </div>
      <div class="tile__actions" (click)="$event.stopPropagation()">
        <button type="button" class="tile__action" title="Move to folder" (click)="move.emit(document().id)">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M4 7h7l2 2h9v10H3z" />
            <path d="M9 13l3 3 3-3M12 16V9" />
          </svg>
        </button>
        <button type="button" class="tile__action tile__action--danger" title="Delete" (click)="delete.emit(document().id)">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 7h14M9 7V5h6v2M7 7l1 13h8l1-13" /></svg>
        </button>
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
        width: 46px;
        height: 46px;
        flex: 0 0 auto;
        place-items: center;
        border-radius: 10px;
        background: #eef2ff;
        color: #4f46e5;
      }
      .tile__icon svg {
        width: 24px;
        height: 24px;
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
        overflow: hidden;
        color: var(--text, #0f172a);
        font-size: 14px;
        font-weight: 800;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .tile__summary {
        margin: 4px 0 0;
        overflow: hidden;
        color: var(--muted, #64748b);
        font-size: 12px;
        line-height: 1.45;
        display: -webkit-box;
        -webkit-line-clamp: 2;
        -webkit-box-orient: vertical;
      }
      .tile__footer {
        margin-top: 10px;
      }
      .tile__actions {
        display: flex;
        gap: 4px;
        opacity: 0;
        transition: opacity 120ms ease;
      }
      .tile--document:hover .tile__actions,
      .tile--document:focus-within .tile__actions {
        opacity: 1;
      }
      .tile__action {
        display: grid;
        width: 32px;
        height: 32px;
        place-items: center;
        border: 1px solid var(--border, #e2e8f0);
        border-radius: 8px;
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
        width: 16px;
        height: 16px;
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
  readonly move = output<string>();
  readonly delete = output<string>();
}
