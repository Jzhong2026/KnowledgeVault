import { Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { LowerCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, forkJoin, of, map, catchError } from 'rxjs';

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
import { MermaidDiagramsDirective } from '../../../shared/directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../../shared/pipes/markdown-content.pipe';
import { KnowledgeEditor } from '../components/knowledge-editor/knowledge-editor';
import { TileGrid } from '../components/tile-grid/tile-grid';

interface MoveTarget {
  id: string | null;
  name: string;
  depth: number;
}

interface ProjectGroup {
  project: ProjectSummary;
  folders: FolderSummary[];
  documents: KnowledgeItemSummary[];
  page: number;
  pageSize: number;
  loading: boolean;
  error: string | null;
}

const GROUP_PAGE_SIZE = 8;

@Component({
  selector: 'app-workspace-page',
  imports: [
    FormsModule,
    LoadingIndicator,
    EmptyState,
    KnowledgeEditor,
    TileGrid,
    MarkdownContentPipe,
    MermaidDiagramsDirective,
    LowerCasePipe,
  ],
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

  readonly projectGroups = signal<ProjectGroup[]>([]);

  readonly projectId = signal<string | null>(null);

  readonly hasFollowedProjects = computed(() => this.projects().length > 0);
  readonly noFollowedProjects = computed(() => this.isProjectScope && this.projects().length === 0);
  readonly canCreate = computed(() => !this.isProjectScope || this.hasFollowedProjects());
  readonly showGroups = computed(
    () =>
      this.isProjectScope &&
      !this.projectId() &&
      !this.workspace.isWorkspaceMode() &&
      this.projects().length > 0,
  );

  readonly editorOpen = signal(false);
  readonly selectedItem = signal<KnowledgeItem | null>(null);
  readonly selectedId = signal<string | null>(null);

  readonly createFolderOpen = signal(false);
  readonly createFolderName = signal('');
  readonly createFolderDescription = signal('');
  readonly createFolderProjectId = signal<string>('');

  readonly renameFolderOpen = signal(false);
  readonly renameFolderId = signal<string | null>(null);
  readonly renameFolderName = signal('');

  readonly deleteFolderId = signal<string | null>(null);

  readonly moveDocOpen = signal(false);
  readonly moveDocId = signal<string | null>(null);
  readonly moveTargets = signal<MoveTarget[]>([]);

  readonly activeDocument = signal<KnowledgeItem | null>(null);
  readonly activeDocumentLoading = signal(false);
  readonly activeDocumentError = signal<string | null>(null);

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
          const root = rootFolderId ?? folderId;
          this.workspace.enterWorkspace({
            scope: this.workspaceScope,
            projectId: projectId ?? null,
            workspaceRootFolderId: root,
            currentFolderId: folderId ?? root,
          });
        } else if (this.isProjectScope && !projectId) {
          if (this.workspace.isWorkspaceMode()) {
            this.workspace.exitWorkspace();
          }
        } else {
          this.workspace.restore(this.workspaceScope, projectId ?? null);
        }
      }),
    );

    effect(() => {
      const projectId = this.projectId();
      const state = this.workspace.current();
      const currentFolderId = this.workspace.currentFolderId();
      void projectId;
      void currentFolderId;
      if (this.showGroups()) {
        this.loadGroups();
      } else {
        this.loadContent(state);
      }
    });

    // Whenever the active tab changes, fetch the corresponding document.
    effect(() => {
      const tab = this.workspace.activeTab();
      if (!tab) {
        this.activeDocument.set(null);
        this.activeDocumentError.set(null);
        return;
      }
      this.activeDocumentLoading.set(true);
      this.activeDocumentError.set(null);
      this.api.getKnowledgeItem(tab.documentId).subscribe({
        next: (item) => this.activeDocument.set(item),
        error: (err: unknown) => this.activeDocumentError.set(getErrorMessage(err)),
        complete: () => this.activeDocumentLoading.set(false),
      });
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
      },
      error: () => {
        /* reference data is non-critical */
      },
    });
  }

  private loadContent(state: WorkspaceState | null): void {
    if (this.isProjectScope && !this.projectId() && !state) {
      this.loading.set(false);
      this.folders.set([]);
      this.documents.set([]);
      this.workspace.setTree(null);
      this.workspace.setBreadcrumb([]);
      return;
    }

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
      error: (err: unknown) => this.error.set(getErrorMessage(err)),
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

  // ----- Grouped (All followed projects) view -----
  private loadGroups(): void {
    const projects = this.projects();
    this.loading.set(true);
    this.error.set(null);
    if (projects.length === 0) {
      this.projectGroups.set([]);
      this.loading.set(false);
      return;
    }

    this.projectGroups.set(
      projects.map((p) => ({
        project: p,
        folders: [],
        documents: [],
        page: 1,
        pageSize: GROUP_PAGE_SIZE,
        loading: true,
        error: null,
      })),
    );

    forkJoin(
      projects.map((p) =>
        this.api.listFolderContent({ scope: this.workspaceScope, projectId: p.id }).pipe(
          map((content) => ({ id: p.id, content, error: null as string | null })),
          catchError((err: unknown) => of({ id: p.id, content: null, error: getErrorMessage(err) })),
        ),
      ),
    ).subscribe((results) => {
      this.projectGroups.update((groups) =>
        groups.map((g) => {
          const r = results.find((x) => x.id === g.project.id);
          if (!r || r.error || !r.content) {
            return { ...g, loading: false, error: r?.error ?? 'Failed to load.' };
          }
          return {
            ...g,
            loading: false,
            folders: r.content!.folders,
            documents: r.content!.documents,
          };
        }),
      );
      this.loading.set(false);
    });
  }

  private reload(): void {
    if (this.showGroups()) {
      this.loadGroups();
    } else {
      this.loadContent(this.workspace.current());
    }
  }

  groupDocuments(group: ProjectGroup): KnowledgeItemSummary[] {
    const start = (group.page - 1) * group.pageSize;
    return group.documents.slice(start, start + group.pageSize);
  }

  groupTotalPages(group: ProjectGroup): number {
    return Math.max(1, Math.ceil(group.documents.length / group.pageSize));
  }

  setGroupPage(projectId: string, page: number): void {
    this.projectGroups.update((groups) =>
      groups.map((g) => (g.project.id === projectId ? { ...g, page } : g)),
    );
  }

  openGroupFolder(projectId: string, folderId: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { projectId, workspaceRootFolderId: folderId, folderId },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  openGroupWorkspace(projectId: string, folderId: string): void {
    this.openGroupFolder(projectId, folderId);
  }

  openGroupFirstWorkspace(group: ProjectGroup): void {
    const firstFolder = group.folders[0];
    if (!firstFolder) {
      return;
    }
    this.openGroupWorkspace(group.project.id, firstFolder.id);
  }

  private findFolder(id: string): FolderSummary | undefined {
    return (
      this.folders().find((f) => f.id === id) ??
      this.projectGroups().flatMap((g) => g.folders).find((f) => f.id === id)
    );
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

  onTreeNavigate(folderId: string): void {
    // Clicking a folder in the workspace tree should NOT enter workspace
    // mode — it only switches which folder is shown. If we are already in a
    // workspace, update its currentFolderId in place and persist; otherwise
    // this is a no-op for outside-workspace navigation.
    const state = this.workspace.current();
    if (!state) {
      return;
    }
    this.workspace.enterWorkspace({ ...state, currentFolderId: folderId });
  }

  closeTab(event: MouseEvent, tabId: string): void {
    event.stopPropagation();
    this.workspace.closeTab(tabId);
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
    if (!this.canCreate()) {
      this.error.set(
        this.noFollowedProjects()
          ? 'Follow a project before creating folders.'
          : 'Pick a project before creating a folder.',
      );
      return;
    }
    this.createFolderName.set('');
    this.createFolderDescription.set('');
    this.createFolderProjectId.set(this.projectId() ?? '');
    this.createFolderOpen.set(true);
  }

  submitCreateFolder(): void {
    const name = this.createFolderName().trim();
    if (!name) {
      return;
    }
    const projectId = this.isProjectScope
      ? (this.createFolderProjectId().trim() || null)
      : null;
    if (this.isProjectScope && !projectId) {
      this.error.set('Pick a project for this folder.');
      return;
    }
    this.saving.set(true);
    const state = this.workspace.current();
    this.api
      .createFolder({
        scope: this.workspaceScope,
        projectId,
        parentFolderId: state ? state.currentFolderId : null,
        name,
        description: this.createFolderDescription().trim() || null,
      })
      .subscribe({
        next: () => {
          this.createFolderOpen.set(false);
          this.saving.set(false);
          this.reload();
        },
        error: (err: unknown) => {
          this.error.set(getErrorMessage(err));
          this.saving.set(false);
        },
      });
  }

  openRenameFolder(id: string): void {
    const folder = this.findFolder(id);
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
        this.reload();
      },
      error: (err: unknown) => {
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
        if (this.showGroups()) {
          this.loadGroups();
          return;
        }
        const state = this.workspace.current();
        if (state && state.workspaceRootFolderId === id) {
          this.exitWorkspace();
        } else if (state && state.currentFolderId === id) {
          this.workspace.setCurrentFolder(state.workspaceRootFolderId);
        } else {
          this.loadContent(state);
        }
      },
      error: (err: unknown) => {
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
  openDocument(idOrSummary: string | KnowledgeItemSummary, title?: string): void {
    const id = typeof idOrSummary === 'string' ? idOrSummary : idOrSummary.id;
    const resolvedTitle = typeof idOrSummary === 'string'
      ? (title ?? '')
      : idOrSummary.title;
    if (this.workspace.isWorkspaceMode()) {
      this.workspace.openDocumentTab(id, resolvedTitle);
      return;
    }
    const route = this.workspaceScope === 'Project' ? '/project-documents/detail' : '/knowledge/detail';
    void this.router.navigate([route, id], { replaceUrl: true });
  }

  openMoveDocument(id: string, projectId?: string): void {
    this.moveDocId.set(id);
    const pid = projectId ?? this.projectId() ?? null;
    this.api.getFolderTree({ scope: this.workspaceScope, projectId: pid }).subscribe({
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
        this.reload();
      },
      error: (err: unknown) => {
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
        this.reload();
      },
      error: (err: unknown) => {
        this.error.set(getErrorMessage(err));
        this.saving.set(false);
      },
    });
  }

  // ----- Editor -----
  createNew(): void {
    if (!this.canCreate()) {
      this.error.set(
        this.noFollowedProjects()
          ? 'Follow a project before creating project documents.'
          : 'Pick a project before creating a document.',
      );
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
    const folderId = item ? undefined : state ? state.currentFolderId : null;
    const payload: SaveDocumentRequest = { ...request, folderId };
    const operation = item
      ? this.api.updateKnowledgeItem(item.id, payload)
      : this.api.createKnowledgeItem(payload);

    operation.subscribe({
      next: () => {
        this.editorOpen.set(false);
        this.saving.set(false);
        this.reload();
      },
      error: (err: unknown) => {
        this.error.set(getErrorMessage(err));
        this.saving.set(false);
      },
    });
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }
}