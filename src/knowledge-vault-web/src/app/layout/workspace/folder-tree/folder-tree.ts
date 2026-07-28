import { Component, forwardRef, inject, input, output, signal } from '@angular/core';

import { FolderTreeNode } from '../../../core/models/folder.models';
import { KnowledgeItemSummary } from '../../../core/models/knowledge.models';
import { WorkspaceService } from '../../../core/workspace/workspace.service';

@Component({
  selector: 'app-folder-tree',
  imports: [forwardRef(() => FolderTree)],
  template: `
    <ul class="folder-tree">
      @for (node of nodes(); track node.id) {
        <li class="folder-tree__item">
          <div
            class="folder-tree__row"
            [class.is-current]="node.id === currentFolderId()"
            [class.is-collapsed]="isCollapsed(node.id)"
            [class.is-drop-target]="activeDropFolderId() === node.id"
            (click)="navigate.emit(node.id)"
            (contextmenu)="onContextMenu($event, node.id)"
            (dragover)="onDragOver($event)"
            (dragenter)="onDragEnter($event, node.id)"
            (dragleave)="onDragLeave($event, node.id)"
            (drop)="onDrop($event, node.id)"
          >
            <button
              type="button"
              class="folder-tree__toggle"
              [class.is-expanded]="!isCollapsed(node.id)"
              [attr.aria-label]="isCollapsed(node.id) ? 'Expand folder' : 'Collapse folder'"
              [attr.aria-expanded]="!isCollapsed(node.id)"
              [attr.title]="isCollapsed(node.id) ? 'Expand' : 'Collapse'"
              (click)="$event.stopPropagation(); toggleNode(node.id)"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M9 6l6 6-6 6" />
              </svg>
            </button>
            <span
              class="folder-tree__icon"
              [class.is-current]="node.id === currentFolderId()"
              [class.is-expanded]="!isCollapsed(node.id)"
              aria-hidden="true"
            >
              @if (!isCollapsed(node.id)) {
                <!-- Open-folder glyph: lid tilted up, contents peeking out -->
                <svg viewBox="0 0 24 24">
                  <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                  <path d="M3 7l1.5-3h5l2 3" />
                </svg>
              } @else {
                <!-- Closed-folder glyph -->
                <svg viewBox="0 0 24 24">
                  <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                </svg>
              }
            </span>
            <span class="folder-tree__label" [title]="node.name">{{ node.name }}</span>
            <button
              type="button"
              class="folder-tree__action"
              title="Open Workspace"
              aria-label="Open Workspace"
              (click)="$event.stopPropagation(); openWorkspace.emit(node.id)"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z" />
              </svg>
            </button>
          </div>
          @if (!isCollapsed(node.id) && documentsFor(node.id).length) {
            <ul class="folder-tree__documents" aria-label="Documents">
              @for (doc of documentsFor(node.id); track doc.id) {
                <li>
                  <button
                    type="button"
                    class="folder-tree__doc"
                    (click)="$event.stopPropagation(); openDocument.emit(doc)"
                    (contextmenu)="onDocumentContextMenu($event, doc.id)"
                    [title]="doc.title"
                  >
                    <span class="folder-tree__doc-icon" aria-hidden="true">
                      <svg viewBox="0 0 24 24">
                        <path d="M6 3h9l4 4v14H6zM15 3v5h4M9 13h7M9 17h7" />
                      </svg>
                    </span>
                    <span class="folder-tree__doc-title">{{ doc.title }}</span>
                  </button>
                </li>
              }
            </ul>
          }
          @if (!isCollapsed(node.id) && node.children.length) {
            <app-folder-tree
              [nodes]="node.children"
              [currentFolderId]="currentFolderId()"
              [folderDocuments]="folderDocuments()"
              (navigate)="navigate.emit($event)"
              (openWorkspace)="openWorkspace.emit($event)"
              (openDocument)="openDocument.emit($event)"
              (folderContextMenu)="folderContextMenu.emit($event)"
              (documentContextMenu)="documentContextMenu.emit($event)"
            />
          }
        </li>
      }
    </ul>
  `,
  styles: [
    `
      .folder-tree {
        margin: 0;
        padding: 0;
        list-style: none;
      }
      .folder-tree .folder-tree {
        /* Stronger nesting line so the parent/child relationship is
           visually obvious when a folder is expanded. The line tracks
           the toggle column so every child visibly connects to the
           parent's caret. */
        margin-left: 12px;
        border-left: 1px dashed #d8e2e8;
        padding-left: 5px;
      }
      .folder-tree__row {
        position: relative;
        display: flex;
        align-items: center;
        gap: 5px;
        padding: 5px 7px;
        border-radius: 6px;
        cursor: pointer;
        color: var(--text, #0f172a);
        font-size: 13px;
      }
      .folder-tree__row:hover {
        background: #f1f6f4;
      }
      /* Strong left accent bar for the active folder. Same as the parent
         shell convention. */
      .folder-tree__row.is-current::before {
        content: '';
        position: absolute;
        left: 0;
        top: 7px;
        bottom: 7px;
        width: 2px;
        border-radius: 0 2px 2px 0;
        background: var(--accent-strong, #0f9d76);
      }
      .folder-tree__row.is-current {
        background: #eaf7f1;
        color: var(--accent-strong, #0f9d76);
        font-weight: 800;
      }
      .folder-tree__toggle {
        display: inline-grid;
        width: 16px;
        height: 16px;
        flex: 0 0 16px;
        place-items: center;
        border: 0;
        border-radius: 4px;
        background: transparent;
        /* Keep the collapsed-state affordance visible on unselected child and
           sibling rows. It must not rely on the selected-row tint or hover. */
        color: #64748b;
        cursor: pointer;
        padding: 0;
      }
      .folder-tree__toggle:hover {
        background: #edf5f2;
        color: #0f766e;
      }
      .folder-tree__toggle svg {
        width: 12px;
        height: 12px;
        fill: none;
        stroke: currentColor;
        stroke-width: 2.2;
        stroke-linecap: round;
        stroke-linejoin: round;
        transition: transform 140ms ease, color 140ms ease, stroke-width 140ms ease;
        transform: rotate(0deg);
      }
      /* When expanded the chevron points down (rotated 90°), switches to
         the accent green and a heavier stroke so the open/closed state is
         obvious even at a glance. */
      .folder-tree__toggle.is-expanded {
        background: transparent;
        color: #0f766e;
      }
      .folder-tree__toggle.is-expanded svg {
        transform: rotate(90deg);
        stroke-width: 2.35;
      }
      .folder-tree__row.is-drop-target {
        box-shadow: inset 0 0 0 2px rgba(16, 185, 129, 0.35);
      }
      .folder-tree__icon {
        display: grid;
        width: 16px;
        height: 16px;
        flex: 0 0 16px;
        place-items: center;
        color: #3c9b80;
      }
      .folder-tree__icon svg {
        width: 14px;
        height: 14px;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.8;
        stroke-linecap: round;
        stroke-linejoin: round;
      }
      .folder-tree__icon.is-current {
        color: var(--accent-strong, #0f9d76);
      }
      .folder-tree__label {
        flex: 1;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .folder-tree__action {
        display: grid;
        width: 26px;
        height: 26px;
        place-items: center;
        border: 0;
        border-radius: 6px;
        background: transparent;
        color: var(--muted, #64748b);
        cursor: pointer;
        padding: 0;
        opacity: 0;
        transition: opacity 120ms ease;
      }
      .folder-tree__row:hover .folder-tree__action {
        opacity: 1;
      }
      .folder-tree__action:hover {
        background: #ffffff;
        color: var(--accent-strong, #0f9d76);
      }
      .folder-tree__action svg {
        width: 15px;
        height: 15px;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.8;
        stroke-linecap: round;
        stroke-linejoin: round;
      }
      .folder-tree__documents {
        margin: 2px 0 6px 32px;
        padding: 0;
        list-style: none;
        display: grid;
        gap: 2px;
      }
      .folder-tree__doc {
        display: flex;
        align-items: center;
        gap: 6px;
        width: 100%;
        border: 0;
        border-radius: 6px;
        background: transparent;
        color: var(--muted, #64748b);
        cursor: pointer;
        font: inherit;
        font-size: 12px;
        padding: 4px 6px;
        text-align: left;
      }
      .folder-tree__doc:hover {
        background: #eef2f7;
        color: var(--text, #0f172a);
      }
      .folder-tree__doc-icon {
        display: inline-grid;
        width: 14px;
        height: 14px;
        flex: 0 0 14px;
        place-items: center;
      }
      .folder-tree__doc-icon svg {
        width: 12px;
        height: 12px;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.8;
        stroke-linecap: round;
        stroke-linejoin: round;
      }
      .folder-tree__doc-title {
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
    `,
  ],
})
export class FolderTree {
  private readonly workspace = inject(WorkspaceService);

  readonly nodes = input.required<FolderTreeNode[]>();
  readonly currentFolderId = input<string | null>(null);
  readonly folderDocuments = input<ReadonlyMap<string, KnowledgeItemSummary[]>>(new Map());
  readonly navigate = output<string>();
  readonly openWorkspace = output<string>();
  readonly openDocument = output<KnowledgeItemSummary>();
  readonly folderContextMenu = output<{ folderId: string; x: number; y: number }>();
  readonly documentContextMenu = output<{ documentId: string; x: number; y: number }>();
  readonly activeDropFolderId = signal<string | null>(null);

  isCurrent(folderId: string): boolean {
    return this.currentFolderId() === folderId;
  }

  documentsFor(folderId: string): KnowledgeItemSummary[] {
    return this.folderDocuments().get(folderId) ?? [];
  }

  isCollapsed(folderId: string): boolean {
    return this.workspace.isNodeCollapsed(folderId);
  }

  toggleNode(folderId: string): void {
    this.workspace.toggleCollapsedNode(folderId);
  }

  onContextMenu(event: MouseEvent, folderId: string): void {
    event.preventDefault();
    event.stopPropagation();
    this.folderContextMenu.emit({ folderId, x: event.clientX, y: event.clientY });
  }

  onDocumentContextMenu(event: MouseEvent, documentId: string): void {
    event.preventDefault();
    event.stopPropagation();
    this.documentContextMenu.emit({ documentId, x: event.clientX, y: event.clientY });
  }

  onDragOver(event: DragEvent): void {
    if (!event.dataTransfer) {
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }

  onDragEnter(event: DragEvent, folderId: string): void {
    event.preventDefault();
    this.activeDropFolderId.set(folderId);
  }

  onDragLeave(_event: DragEvent, folderId: string): void {
    if (this.activeDropFolderId() === folderId) {
      this.activeDropFolderId.set(null);
    }
  }

  onDrop(event: DragEvent, folderId: string): void {
    event.preventDefault();
    this.activeDropFolderId.set(null);
    const documentId = event.dataTransfer?.getData('application/x-kv-document-id')
      ?? event.dataTransfer?.getData('text/plain')
      ?? '';
    if (!documentId) {
      return;
    }
    this.workspace.requestDocumentMove(documentId, folderId);
  }
}
