import { Component, input, output, signal } from '@angular/core';

import { FolderSummary } from '../../../../core/models/folder.models';

@Component({
  selector: 'app-folder-tile',
  template: `
    <article
      class="tile tile--folder"
      [class.tile--current]="isCurrent()"
      [class.tile--drop-target]="isDropActive()"
      [class.tile--selected]="selected()"
      (click)="open.emit(folder().id)"
      (dragover)="onDragOver($event)"
      (dragenter)="onDragEnter($event)"
      (dragleave)="onDragLeave($event)"
      (drop)="onDrop($event)"
    >
      <label class="tile__selection" (pointerdown)="stopSelectionInteraction($event)" (click)="stopSelectionInteraction($event)">
        <input type="checkbox" [checked]="selected()" (click)="stopSelectionInteraction($event)" (change)="onSelectionChange($event)" [attr.aria-label]="'Select folder ' + folder().name" />
      </label>
      <div class="tile__icon tile__icon--folder" aria-hidden="true">
        <svg viewBox="0 0 24 24">
          <path d="M3 6a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
          <path d="M3 10h18" />
        </svg>
      </div>
      <div class="tile__body">
        <h3 class="tile__title" [title]="folder().name">{{ folder().name }}</h3>
        @if (folder().isArchived) {
          <div class="tile__status">Archived</div>
        } @else {
          <div class="tile__status tile__status--placeholder" aria-hidden="true"></div>
        }
        <div class="tile__creator" [title]="folder().creatorDisplayName || 'Unknown'">
          Creator: {{ folder().creatorDisplayName || 'Unknown' }}
        </div>
      </div>
      <div class="tile__actions" (click)="$event.stopPropagation()">
        <button
          type="button"
          class="tile__action"
          title="Download folder"
          aria-label="Download folder"
          (click)="download.emit(folder().id)"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M12 6v9" />
            <path d="M8.5 12.5 12 16l3.5-3.5" />
            <path d="M5 19h14" />
          </svg>
        </button>
        <button
          type="button"
          class="tile__action"
          title="Open Workspace"
          aria-label="Open Workspace"
          (click)="openWorkspace.emit(folder().id)"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z" /></svg>
        </button>
        @if (folder().isArchived) {
          <button type="button" class="tile__action" title="Restore" (click)="restore.emit(folder().id)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 18V8M8 12l4-4 4 4M5 5h14v14H5z" /></svg>
          </button>
        } @else {
          <button type="button" class="tile__action" title="Rename" (click)="rename.emit(folder().id)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 20h4L19 9l-4-4L4 16zM14 6l4 4" /></svg>
          </button>
          <button type="button" class="tile__action tile__action--danger" title="Archive" (click)="delete.emit(folder().id)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 5h14v14H5zM8 5v-2h8v2M8 12h8" /></svg>
          </button>
        }
      </div>
    </article>
  `,
  styles: [
    `
      .tile--folder {
        cursor: pointer;
      }
      .tile--drop-target {
        border-color: var(--accent, #10b981);
        box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.2);
      }
      .tile__icon {
        display: grid;
        width: 34px;
        height: 34px;
        flex: 0 0 auto;
        place-items: center;
        border-radius: 8px;
      }
      .tile__selection {
        display: grid;
        width: 24px;
        height: 34px;
        flex: 0 0 auto;
        place-items: center;
        cursor: pointer;
      }
      .tile__selection input {
        width: 16px;
        height: 16px;
        margin: 0;
        accent-color: var(--accent, #10b981);
        cursor: pointer;
      }
      .tile__icon--folder {
        background: #fef3c7;
        color: #b45309;
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
      .tile__status--placeholder {
        min-height: 14px;
      }
      .tile__creator {
        margin-top: 3px;
        color: var(--muted, #64748b);
        font-size: 11px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .tile__actions {
        display: flex;
        grid-column: 1 / -1;
        justify-content: flex-end;
        gap: 4px;
        width: 100%;
        max-height: 0;
        overflow: hidden;
        opacity: 0;
        transition:
          max-height 140ms ease,
          opacity 120ms ease;
      }
      .tile--folder:hover .tile__actions,
      .tile--folder:focus-within .tile__actions {
        max-height: 30px;
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
export class FolderTile {
  readonly folder = input.required<FolderSummary>();
  readonly isCurrent = input(false);
  readonly selected = input(false);
  readonly open = output<string>();
  readonly selectionChange = output<boolean>();
  readonly download = output<string>();
  readonly openWorkspace = output<string>();
  readonly rename = output<string>();
  readonly delete = output<string>();
  readonly restore = output<string>();
  readonly moveDocumentToFolder = output<{ documentId: string; folderId: string }>();

  private dragCounter = 0;
  readonly isDropActive = signal(false);

  stopSelectionInteraction(event: Event): void {
    event.stopPropagation();
  }

  onSelectionChange(event: Event): void {
    event.stopPropagation();
    this.selectionChange.emit((event.target as HTMLInputElement).checked);
  }

  onDragOver(event: DragEvent): void {
    if (!event.dataTransfer) {
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }

  onDragEnter(event: DragEvent): void {
    event.preventDefault();
    this.dragCounter += 1;
    this.isDropActive.set(true);
  }

  onDragLeave(_event: DragEvent): void {
    this.dragCounter = Math.max(0, this.dragCounter - 1);
    if (this.dragCounter === 0) {
      this.isDropActive.set(false);
    }
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragCounter = 0;
    this.isDropActive.set(false);
    const documentId = event.dataTransfer?.getData('application/x-kv-document-id')
      ?? event.dataTransfer?.getData('text/plain')
      ?? '';
    if (!documentId) {
      return;
    }
    this.moveDocumentToFolder.emit({ documentId, folderId: this.folder().id });
  }
}
