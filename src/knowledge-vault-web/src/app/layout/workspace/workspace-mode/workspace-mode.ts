import { Component, inject } from '@angular/core';

import { WorkspaceService } from '../../../core/workspace/workspace.service';
import { FolderTree } from '../folder-tree/folder-tree';

@Component({
  selector: 'app-workspace-mode',
  imports: [FolderTree],
  template: `
    <aside class="workspace-mode">
      <div class="workspace-mode__header">
        <span class="workspace-mode__title">Workspace</span>
        <button type="button" class="workspace-mode__exit" (click)="exit()">Exit</button>
      </div>
      <div class="workspace-mode__tree">
        @if (tree(); as root) {
          @if (root.children.length) {
            <app-folder-tree
              [nodes]="root.children"
              [currentFolderId]="currentFolderId()"
              (navigate)="onNavigate($event)"
              (openWorkspace)="onOpenWorkspace($event)"
            />
          } @else {
            <p class="workspace-mode__empty">No subfolders in this workspace yet.</p>
          }
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
      }
      .workspace-mode__title {
        font-size: 12px;
        font-weight: 800;
        letter-spacing: 0.06em;
        text-transform: uppercase;
        color: var(--muted, #64748b);
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
  readonly tree = this.workspace.folderTree;
  readonly currentFolderId = this.workspace.currentFolderId;

  exit(): void {
    this.workspace.exitWorkspace();
  }

  onNavigate(folderId: string): void {
    this.workspace.setCurrentFolder(folderId);
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
}
