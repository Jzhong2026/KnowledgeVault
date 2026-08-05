import { Component, input, output } from '@angular/core';

import { FolderSummary } from '../../../../core/models/folder.models';
import { KnowledgeItemSummary } from '../../../../core/models/knowledge.models';
import { DocumentTile } from '../document-tile/document-tile';
import { FolderTile } from '../folder-tile/folder-tile';

@Component({
  selector: 'app-tile-grid',
  imports: [FolderTile, DocumentTile],
  template: `
    <div class="tile-grid">
      @for (folder of folders(); track folder.id) {
        <app-folder-tile
          [folder]="folder"
          [isCurrent]="folder.id === currentFolderId()"
          [selected]="selectedFolderIds().has(folder.id)"
          (open)="openFolder.emit($event)"
          (selectionChange)="folderSelectionChange.emit({ id: folder.id, selected: $event })"
          (download)="downloadFolder.emit($event)"
          (openWorkspace)="openWorkspace.emit($event)"
          (rename)="renameFolder.emit($event)"
          (delete)="deleteFolder.emit($event)"
          (restore)="restoreFolder.emit($event)"
          (moveDocumentToFolder)="moveDocumentToFolder.emit($event)"
        />
      }
      @for (document of documents(); track document.id) {
        <app-document-tile
          [document]="document"
          [selected]="selectedDocumentIds().has(document.id)"
          (open)="openDocument.emit(document)"
          (selectionChange)="documentSelectionChange.emit({ id: document.id, selected: $event })"
          (download)="downloadDocument.emit($event)"
          (delete)="deleteDocument.emit($event)"
          (restore)="restoreDocument.emit($event)"
        />
      }
    </div>
  `,
  styles: [
    `
      .tile-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
        gap: 12px;
        align-content: start;
      }
      :host ::ng-deep .tile {
        display: grid;
        grid-template-columns: auto auto minmax(0, 1fr);
        gap: 12px;
        align-items: center;
        padding: 12px;
        border: 1px solid var(--border, #e2e8f0);
        border-radius: 10px;
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
      :host ::ng-deep .tile--selected {
        border-color: var(--accent, #10b981);
        background: #ecfdf5;
      }
    `,
  ],
})
export class TileGrid {
  readonly folders = input<FolderSummary[]>([]);
  readonly documents = input<KnowledgeItemSummary[]>([]);
  readonly currentFolderId = input<string | null>(null);
  readonly selectedFolderIds = input<ReadonlySet<string>>(new Set());
  readonly selectedDocumentIds = input<ReadonlySet<string>>(new Set());
  readonly openFolder = output<string>();
  readonly folderSelectionChange = output<{ id: string; selected: boolean }>();
  readonly openWorkspace = output<string>();
  readonly renameFolder = output<string>();
  readonly deleteFolder = output<string>();
  readonly restoreFolder = output<string>();
  readonly openDocument = output<KnowledgeItemSummary>();
  readonly documentSelectionChange = output<{ id: string; selected: boolean }>();
  readonly downloadDocument = output<string>();
  readonly downloadFolder = output<string>();
  readonly moveDocumentToFolder = output<{ documentId: string; folderId: string }>();
  readonly deleteDocument = output<string>();
  readonly restoreDocument = output<string>();
}
