import { Component, computed, input, output } from '@angular/core';

import { FolderSummary } from '../../../../core/models/folder.models';
import { KnowledgeItemSummary } from '../../../../core/models/knowledge.models';
import { DocumentTile } from '../document-tile/document-tile';
import { FolderTile } from '../folder-tile/folder-tile';

@Component({
  selector: 'app-tile-grid',
  imports: [FolderTile, DocumentTile],
  template: `
    <div class="tile-grid">
      @for (folder of sortedFolders(); track folder.id) {
        <app-folder-tile
          [folder]="folder"
          [isCurrent]="folder.id === currentFolderId()"
          (open)="openFolder.emit($event)"
          (openWorkspace)="openWorkspace.emit($event)"
          (rename)="renameFolder.emit($event)"
          (delete)="deleteFolder.emit($event)"
        />
      }
      @for (document of sortedDocuments(); track document.id) {
        <app-document-tile
          [document]="document"
          (open)="openDocument.emit($event)"
          (move)="moveDocument.emit($event)"
          (delete)="deleteDocument.emit($event)"
        />
      }
    </div>
  `,
  styles: [
    `
      .tile-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
        gap: 14px;
      }
      :host ::ng-deep .tile {
        display: flex;
        gap: 12px;
        align-items: flex-start;
        padding: 14px;
        border: 1px solid var(--border, #e2e8f0);
        border-radius: 12px;
        background: #ffffff;
        transition:
          border-color 140ms ease,
          box-shadow 140ms ease,
          transform 140ms ease;
      }
      :host ::ng-deep .tile:hover {
        border-color: var(--accent, #10b981);
        box-shadow: 0 8px 22px rgba(15, 23, 42, 0.08);
        transform: translateY(-1px);
      }
      :host ::ng-deep .tile--current {
        border-color: var(--accent, #10b981);
        background: #f4fbf8;
      }
    `,
  ],
})
export class TileGrid {
  readonly folders = input<FolderSummary[]>([]);
  readonly documents = input<KnowledgeItemSummary[]>([]);
  readonly currentFolderId = input<string | null>(null);
  readonly openFolder = output<string>();
  readonly openWorkspace = output<string>();
  readonly renameFolder = output<string>();
  readonly deleteFolder = output<string>();
  readonly openDocument = output<string>();
  readonly moveDocument = output<string>();
  readonly deleteDocument = output<string>();

  readonly sortedFolders = computed(() =>
    [...this.folders()].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name)),
  );

  readonly sortedDocuments = computed(() => [...this.documents()].sort((a, b) => a.title.localeCompare(b.title)));
}
