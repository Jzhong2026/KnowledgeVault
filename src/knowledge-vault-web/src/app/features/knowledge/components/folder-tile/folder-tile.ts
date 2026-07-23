import { Component, input, output } from '@angular/core';

import { FolderSummary } from '../../../../core/models/folder.models';

@Component({
  selector: 'app-folder-tile',
  template: `
    <article class="tile tile--folder" [class.tile--current]="isCurrent()" (click)="open.emit(folder().id)">
      <div class="tile__icon" aria-hidden="true">
        <svg viewBox="0 0 24 24"><path d="M3 7h7l2 2h9v10H3zM3 7V5h7l2 2" /></svg>
      </div>
      <div class="tile__body">
        <h3 class="tile__title" [title]="folder().name">{{ folder().name }}</h3>
        <p class="tile__meta">
          {{ folder().childFolderCount }} {{ folder().childFolderCount === 1 ? 'subfolder' : 'subfolders' }}
          &middot;
          {{ folder().documentCount }} {{ folder().documentCount === 1 ? 'doc' : 'docs' }}
        </p>
      </div>
      <div class="tile__actions" (click)="$event.stopPropagation()">
        <button
          type="button"
          class="tile__action"
          title="Open Workspace"
          aria-label="Open Workspace"
          (click)="openWorkspace.emit(folder().id)"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z" /></svg>
        </button>
        <button type="button" class="tile__action" title="Rename" (click)="rename.emit(folder().id)">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 20h4L19 9l-4-4L4 16zM14 6l4 4" /></svg>
        </button>
        <button type="button" class="tile__action tile__action--danger" title="Delete" (click)="delete.emit(folder().id)">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 7h14M9 7V5h6v2M7 7l1 13h8l1-13" /></svg>
        </button>
      </div>
    </article>
  `,
  styles: [
    `
      .tile--folder {
        cursor: pointer;
      }
      .tile__icon {
        display: grid;
        width: 46px;
        height: 46px;
        flex: 0 0 auto;
        place-items: center;
        border-radius: 10px;
        background: #edf7f3;
        color: var(--accent-strong, #0f9d76);
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
      .tile__meta {
        margin: 4px 0 0;
        color: var(--muted, #64748b);
        font-size: 12px;
      }
      .tile__actions {
        display: flex;
        gap: 4px;
        opacity: 0;
        transition: opacity 120ms ease;
      }
      .tile--folder:hover .tile__actions,
      .tile--folder:focus-within .tile__actions {
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
export class FolderTile {
  readonly folder = input.required<FolderSummary>();
  readonly isCurrent = input(false);
  readonly open = output<string>();
  readonly openWorkspace = output<string>();
  readonly rename = output<string>();
  readonly delete = output<string>();
}
