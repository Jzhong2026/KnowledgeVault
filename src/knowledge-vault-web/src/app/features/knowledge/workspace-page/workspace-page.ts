import { Component, OnDestroy, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import {
  Category,
  DocumentScope,
  KnowledgeItem,
  KnowledgeItemSummary,
  SaveDocumentRequest,
  Tag,
} from '../../../core/models/knowledge.models';
import { FolderSummary, FolderTreeNode } from '../../../core/models/folder.models';
import { ProjectSummary, ProjectTopic } from '../../../core/models/projects.models';
import { BreadcrumbNode, WorkspaceService, WorkspaceState } from '../../../core/workspace/workspace.service';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { KnowledgeEditor } from '../components/knowledge-editor/knowledge-editor';
import { TileGrid } from '../components/tile-grid/tile-grid';

interface MoveTarget {
  id: string | null;
  name: string;
  depth: number;
}

@Component({
  selector: 'app-workspace-page',
  imports: [FormsModule, LoadingIndicator, EmptyState, KnowledgeEditor, TileGrid],
  templateUrl: './workspace-page.html',
  styleUrl: './workspace-page.css',
})
export class WorkspacePage implements OnDestroy {
  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly workspace = inject(WorkspaceService);

  readonly workspaceScope = (this.route.snapshot.data['scope'] as DocumentScope | undefined) ?? 'Personal';
  readonly isProjectScope = this.workspaceScope === 'Project';

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly folders = signal<FolderSummary[]>([]);
  readonly documents = signal<KnowledgeItemSummary[]>([]);
  readonly projects = signal<ProjectSummary[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly tags = signal<Tag[]>([]);
  readonly editorTopics = signal<ProjectTopic[]>([]);

  readonly projectId = signal<string | null>(null);

  readonly editorOpen = signal(false);
  readonly selectedItem = signal<KnowledgeItem | null>(null);
  readonly selectedId = signal<string | null>(null);

  readonly createFolderOpen = signal(false);
  readonly createFolderName = signal('');
  readonly createFolderDescription = signal('');

  readonly renameFolderOpen = signal(false);
  readonly renameFolderId = signal<string | null>(null);
  readonly renameFolderName = signal('');

  readonly deleteFolderId = signal<string | null>(null);

  readonly moveDocOpen = signal(false);
  readonly moveDocId = signal<string | null>(null);
  readonly moveTargets = signal<MoveTarget[]>([]);

  private readonly sub = new Subscription();

  constructor() {
    this.sub.add(
      this.route.queryParamMap.subscribe((params) => {
        const projectId = params.get('projectId');
        const rootFolderId = params.get('workspaceRootFolderId');
        const folderId = params.get('folderId');

        if (projectId !== this.projectId()) {
          this.projectId.set(projectId);
        }

        if (rootFolderId || folderId) {
          // Deep link overrides any restored state for this session.
          const root = rootFolderId ?? folderId;
          this.workspace.enterWorkspace({
            scope: this.workspaceScope,
            projectId: projectId ?? null,
            workspaceRootFolderId: root,
            currentFolderId: folderId ?? root,
          });
        } else {
          // No deep link: restore the remembered workspace for this
          // scope+project. Required for Personal scope where projectId
          // stays null, so it must not be gated behind a projectId change.
          this.workspace.restore(this.workspaceScope, projectId ?? null);
        }
      }),
    );

    // Loading is driven entirely by workspace state changes.
    effect(() => {
      const projectId = this.projectId();
      const state = this.workspace.current();
      const currentFolderId = this.workspace.currentFolderId();
      void projectId;
      void currentFolderId;
      this.loadContent(state);
    });

    this.loadReferenceData();
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  private loadReferenceData(): void {
    const projects$ = this.isProjectScope
      ? this.api.listProjects({ followingOnly: true, pageSize: 100 })
      : of({ items: [] as ProjectSummary[], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 });

    forkJoin({
      categories: this.api.listCategories(),
      tags: this.api.listTags(),
      projects: projects$,
    }).subscribe({
      next: ({ categories, tags, projects }) => {
        this.categories.set(categories);
        this.tags.set(tags);
        this.projects.set(projects.items);
        if (this.isProjectScope && !this.projectId() && projects.items.length === 1) {
          this.onProjectChange(projects.items[0].id);
        }
      },
      error: () => {
        /* reference data is non-critical */
      },
    });
  }

  private loadContent(state: WorkspaceState | null): void {
    this.loading.set(true);
    this.error.set(null);

    const inWorkspace = state !== null;
    const parentFolderId = inWorkspace ? state!.currentFolderId : null;
    const rootFolderId = inWorkspace ? state!.workspaceRootFolderId : null;

    const content$ = this.api.listFolderContent({
      scope: this.workspaceScope,
      projectId: this.projectId() ?? null,
      parentFolderId,
      rootFolderId,
    });

    const tree$ = inWorkspace
      ? this.api.getFolderTree({
          scope: this.workspaceScope,
          projectId: this.projectId() ?? null,
          rootFolderId: state!.workspaceRootFolderId,
        })
      : of(null);

    forkJoin({ content: content$, tree: tree$ }).subscribe({
      next: ({ content, tree }) => {
        this.folders.set(content.folders);
        this.documents.set(content.documents);
        this.workspace.setTree(tree);
        if (tree) {
          this.workspace.setBreadcrumb(this.buildPath(tree, state!.currentFolderId!));
        } else {
          this.workspace.setBreadcrumb([]);
        }
      },
      error: (err) => this.error.set(getErrorMessage(err)),
      complete: () => this.loading.set(false),
    });
  }

  private buildPath(tree: FolderTreeNode, folderId: string): BreadcrumbNode[] {
    const path: BreadcrumbNode[] = [];
    const dfs = (node: FolderTreeNode): boolean => {
      path.push({ id: node.id, name: node.name });
      if (node.id === folderId) {
        return true;
      }
      for (const child of node.children) {
        if (dfs(child)) {
          return true;
        }
      }
      path.pop();
      return false;
    };
    dfs(tree);
    return path;
  }

  // ----- Navigation -----
  openFolder(folderId: string): void {
    if (this.workspace.isWorkspaceMode()) {
      this.workspace.setCurrentFolder(folderId);
    } else {
      this.workspace.enterWorkspace({
        scope: this.workspaceScope,
        projectId: this.projectId() ?? null,
        workspaceRootFolderId: folderId,
        currentFolderId: folderId,
      });
    }
  }

  openWorkspace(folderId: string): void {
    this.workspace.enterWorkspace({
      scope: this.workspaceScope,
      projectId: this.projectId() ?? null,
      workspaceRootFolderId: folderId,
      currentFolderId: folderId,
    });
  }

  exitWorkspace(): void {
    this.workspace.exitWorkspace();
    void this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  onProjectChange(projectId: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { projectId: projectId || null, workspaceRootFolderId: null, folderId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  // ----- Folder CRUD -----
  openCreateFolder(): void {
    this.createFolderName.set('');
    this.createFolderDescription.set('');
    this.createFolderOpen.set(true);
  }

  submitCreateFolder(): void {
    const name = this.createFolderName().trim();
    if (!name) {
      return;
    }
    this.saving.set(true);
    const state = this.workspace.current();
    this.api
      .createFolder({
        scope: this.workspaceScope,
        projectId: this.projectId() ?? null,
        parentFolderId: state ? state.currentFolderId : null,
        name,
        description: this.createFolderDescription().trim() || null,
      })
      .subscribe({
        next: () => {
          this.createFolderOpen.set(false);
          this.saving.set(false);
          this.loadContent(this.workspace.current());
        },
        error: (err) => {
          this.error.set(getErrorMessage(err));
          this.saving.set(false);
        },
      });
  }

  openRenameFolder(id: string): void {
    const folder = this.folders().find((f) => f.id === id);
    this.renameFolderId.set(id);
    this.renameFolderName.set(folder?.name ?? '');
    this.renameFolderOpen.set(true);
  }

  submitRenameFolder(): void {
    const id = this.renameFolderId();
    const name = this.renameFolderName().trim();
    if (!id || !name) {
      return;
    }
    this.saving.set(true);
    this.api.updateFolder(id, { name }).subscribe({
      next: () => {
        this.renameFolderOpen.set(false);
        this.saving.set(false);
        this.loadContent(this.workspace.current());
      },
      error: (err) => {
        this.error.set(getErrorMessage(err));
        this.saving.set(false);
      },
    });
  }

  confirmDeleteFolder(id: string): void {
    this.deleteFolderId.set(id);
  }

  cancelDeleteFolder(): void {
    this.deleteFolderId.set(null);
  }

  executeDeleteFolder(): void {
    const id = this.deleteFolderId();
    if (!id) {
      return;
    }
    this.saving.set(true);
    this.api.deleteFolder(id).subscribe({
      next: () => {
        this.deleteFolderId.set(null);
        this.saving.set(false);
        const state = this.workspace.current();
        if (state && state.workspaceRootFolderId === id) {
          // The workspace root was deleted: leave workspace mode entirely.
          this.exitWorkspace();
        } else if (state && state.currentFolderId === id) {
          this.workspace.setCurrentFolder(state.workspaceRootFolderId);
        } else {
          this.loadContent(state);
        }
      },
      error: (err) => {
        const message = getErrorMessage(err);
        this.error.set(
          message.includes('409')
            ? 'Cannot delete a folder that still contains items. Move or delete its contents first.'
            : message,
        );
        this.deleteFolderId.set(null);
        this.saving.set(false);
      },
    });
  }

  // ----- Document actions -----
  openDocument(id: string): void {
    const route = this.workspaceScope === 'Project' ? '/project-documents/detail' : '/knowledge/detail';
    void this.router.navigate([route, id], { replaceUrl: true });
  }

  openMoveDocument(id: string): void {
    this.moveDocId.set(id);
    this.api
      .getFolderTree({ scope: this.workspaceScope, projectId: this.projectId() ?? null })
      .subscribe({
        next: (tree) => this.moveTargets.set(this.flattenTree(tree, 0)),
        error: () => this.moveTargets.set([]),
      });
    this.moveDocOpen.set(true);
  }

  private flattenTree(node: FolderTreeNode, depth: number): MoveTarget[] {
    const out: MoveTarget[] = [{ id: node.id, name: node.name, depth }];
    for (const child of node.children) {
      out.push(...this.flattenTree(child, depth + 1));
    }
    return out;
  }

  submitMoveDocument(folderId: string | null): void {
    const id = this.moveDocId();
    if (!id) {
      return;
    }
    this.saving.set(true);
    this.api.moveDocument(id, folderId).subscribe({
      next: () => {
        this.moveDocOpen.set(false);
        this.saving.set(false);
        this.loadContent(this.workspace.current());
      },
      error: (err) => {
        this.error.set(getErrorMessage(err));
        this.saving.set(false);
      },
    });
  }

  deleteDocument(id: string): void {
    if (!confirm('Delete this document? This cannot be undone.')) {
      return;
    }
    this.saving.set(true);
    this.api.deleteKnowledgeItem(id).subscribe({
      next: () => {
        this.saving.set(false);
        this.loadContent(this.workspace.current());
      },
      error: (err) => {
        this.error.set(getErrorMessage(err));
        this.saving.set(false);
      },
    });
  }

  // ----- Editor -----
  createNew(): void {
    if (this.isProjectScope && this.projects().length === 0) {
      this.error.set('Follow a project before creating project documents.');
      return;
    }
    this.selectedId.set(null);
    this.selectedItem.set(null);
    this.editorTopics.set([]);
    this.editorOpen.set(true);
  }

  onEditorProjectSelected(projectId: string): void {
    this.api.listTopics(projectId).subscribe({
      next: (result) => this.editorTopics.set(result.items),
      error: () => this.editorTopics.set([]),
    });
  }

  saveDocument(request: SaveDocumentRequest): void {
    this.saving.set(true);
    const state = this.workspace.current();
    const item = this.selectedItem();
    // New documents are placed in the current workspace folder (or root when
    // outside a workspace). Editing an existing document must preserve its
    // current folder, so we omit folderId on updates.
    const folderId = item ? undefined : state ? state.currentFolderId : null;
    const payload: SaveDocumentRequest = { ...request, folderId };
    const operation = item
      ? this.api.updateKnowledgeItem(item.id, payload)
      : this.api.createKnowledgeItem(payload);

    operation.subscribe({
      next: () => {
        this.editorOpen.set(false);
        this.saving.set(false);
        this.loadContent(this.workspace.current());
      },
      error: (err) => {
        this.error.set(getErrorMessage(err));
        this.saving.set(false);
      },
    });
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }
}
