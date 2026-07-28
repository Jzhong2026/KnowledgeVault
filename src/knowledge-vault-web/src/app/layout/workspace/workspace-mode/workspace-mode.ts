import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { KnowledgeItemSummary } from '../../../core/models/knowledge.models';
import { WorkspaceService } from '../../../core/workspace/workspace.service';
import { FolderTree } from '../folder-tree/folder-tree';

@Component({
  selector: 'app-workspace-mode',
  imports: [FolderTree],
  template: `
    <aside class="workspace-mode">
      <header class="workspace-mode__header">
        <div
          class="workspace-mode__title-block"
          [class.is-drop-target]="isRootDropTarget()"
          (dragover)="onRootDragOver($event)"
          (dragenter)="onRootDragEnter($event)"
          (dragleave)="onRootDragLeave($event)"
          (drop)="onRootDrop($event)"
        >
          <span class="workspace-mode__eyebrow">Workspace</span>
          <span class="workspace-mode__title" [title]="rootName() ?? ''">{{ rootName() ?? '…' }}</span>
        </div>
        <button type="button" class="workspace-mode__exit" (click)="exit()">Exit Workspace</button>
      </header>
      <div class="workspace-mode__tree" (contextmenu)="onRootContextMenu($event)">
        @if (tree(); as root) {
          <app-folder-tree
            [nodes]="[root]"
            [currentFolderId]="currentFolderId()"
            [folderDocuments]="folderDocumentsById()"
            (navigate)="onNavigate($event)"
            (openWorkspace)="onOpenWorkspace($event)"
            (openDocument)="onOpenDocument($event)"
            (folderContextMenu)="onFolderContextMenu($event)"
            (documentContextMenu)="onDocumentContextMenu($event)"
          />
        } @else {
          <p class="workspace-mode__empty">Loading workspace tree…</p>
        }
      </div>
    </aside>
  `,
  styles: [
    `
      .workspace-mode {
        display: flex;
        flex-direction: column;
        width: 100%;
        height: 100%;
        border-right: 1px solid var(--border, #e2e8f0);
        background: #f8fafc;
      }
      .workspace-mode__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 14px 16px;
        border-bottom: 1px solid var(--border, #e2e8f0);
        gap: 8px;
      }
      .workspace-mode__title-block {
        display: flex;
        flex-direction: column;
        min-width: 0;
        border-radius: 8px;
        padding: 4px 6px;
      }
      .workspace-mode__title-block.is-drop-target {
        background: rgba(16, 185, 129, 0.15);
        box-shadow: inset 0 0 0 1px rgba(16, 185, 129, 0.4);
      }
      .workspace-mode__eyebrow {
        font-size: 10px;
        font-weight: 800;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        color: var(--muted, #64748b);
      }
      .workspace-mode__title {
        font-size: 15px;
        font-weight: 800;
        color: var(--text, #0f172a);
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        max-width: 200px;
      }
      .workspace-mode__exit {
        border: 1px solid var(--border, #e2e8f0);
        border-radius: 8px;
        background: #ffffff;
        color: var(--text, #0f172a);
        cursor: pointer;
        font-size: 12px;
        font-weight: 800;
        padding: 6px 12px;
      }
      .workspace-mode__exit:hover {
        border-color: #fecaca;
        color: #dc2626;
      }
      .workspace-mode__tree {
        flex: 1;
        overflow-y: auto;
        padding: 12px 10px;
      }
      .workspace-mode__empty {
        margin: 8px;
        color: var(--muted, #64748b);
        font-size: 13px;
      }
    `,
  ],
})
export class WorkspaceMode {
  private readonly workspace = inject(WorkspaceService);
  private readonly router = inject(Router);
  readonly tree = this.workspace.folderTree;
  readonly currentFolderId = this.workspace.currentFolderId;
  readonly folderDocumentsById = this.workspace.folderDocumentsById;
  readonly rootName = this.workspace.rootName;
  readonly rootFolderId = this.workspace.workspaceRootFolderId;
  readonly isRootDropTarget = signal(false);

  exit(): void {
    this.workspace.exitWorkspace();
    void this.router.navigate([], {
      queryParams: { workspaceRootFolderId: null, folderId: null, browseFolderId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  onNavigate(folderId: string): void {
    // Switching the current folder inside the workspace must not change the
    // workspace state shape or exit workspace mode. setCurrentFolder reuses
    // the existing root + scope so the tree stays valid.
    const state = this.workspace.current();
    if (!state) {
      return;
    }
    this.workspace.enterWorkspace({ ...state, currentFolderId: folderId });
  }

  onOpenWorkspace(folderId: string): void {
    const state = this.workspace.current();
    if (state) {
      this.workspace.enterWorkspace({
        ...state,
        workspaceRootFolderId: folderId,
        currentFolderId: folderId,
      });
    }
  }

  onOpenDocument(doc: KnowledgeItemSummary): void {
    this.workspace.openDocumentTab(doc.id, doc.title);
  }

  onFolderContextMenu(event: { folderId: string; x: number; y: number }): void {
    this.workspace.requestFolderContextMenu(event.folderId, event.x, event.y);
  }

  onDocumentContextMenu(event: { documentId: string; x: number; y: number }): void {
    this.workspace.requestDocumentContextMenu(event.documentId, event.x, event.y);
  }

  onRootContextMenu(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (target.closest('.folder-tree__row, .folder-tree__doc')) {
      return;
    }
    event.preventDefault();
    this.workspace.requestFolderContextMenu(this.rootFolderId(), event.clientX, event.clientY);
  }

  onRootDragOver(event: DragEvent): void {
    if (!event.dataTransfer) {
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }

  onRootDragEnter(event: DragEvent): void {
    event.preventDefault();
    this.isRootDropTarget.set(true);
  }

  onRootDragLeave(_event: DragEvent): void {
    this.isRootDropTarget.set(false);
  }

  onRootDrop(event: DragEvent): void {
    event.preventDefault();
    this.isRootDropTarget.set(false);
    const folderId = this.rootFolderId();
    const documentId = event.dataTransfer?.getData('application/x-kv-document-id')
      ?? event.dataTransfer?.getData('text/plain')
      ?? '';
    if (!documentId || !folderId) {
      return;
    }
    this.workspace.requestDocumentMove(documentId, folderId);
  }
}
