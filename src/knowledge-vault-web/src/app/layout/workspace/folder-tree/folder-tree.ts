import { Component, forwardRef, input, output } from '@angular/core';

import { FolderTreeNode } from '../../../core/models/folder.models';

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
            (click)="navigate.emit(node.id)"
          >
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
          @if (node.children.length) {
            <app-folder-tree
              [nodes]="node.children"
              [currentFolderId]="currentFolderId()"
              (navigate)="navigate.emit($event)"
              (openWorkspace)="openWorkspace.emit($event)"
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
        margin-left: 14px;
        border-left: 1px solid var(--border, #e2e8f0);
        padding-left: 6px;
      }
      .folder-tree__row {
        display: flex;
        align-items: center;
        gap: 6px;
        padding: 6px 8px;
        border-radius: 8px;
        cursor: pointer;
        color: var(--text, #0f172a);
        font-size: 13px;
      }
      .folder-tree__row:hover {
        background: #f1f5f9;
      }
      .folder-tree__row.is-current {
        background: #e7f6ef;
        color: var(--accent-strong, #0f9d76);
        font-weight: 800;
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
    `,
  ],
})
export class FolderTree {
  readonly nodes = input.required<FolderTreeNode[]>();
  readonly currentFolderId = input<string | null>(null);
  readonly navigate = output<string>();
  readonly openWorkspace = output<string>();
}
