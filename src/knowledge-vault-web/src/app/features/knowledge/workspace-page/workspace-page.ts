import { Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { LowerCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { AuthService } from '../../../core/auth/auth.service';
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

/** Page size used for the workspace "Load more" UI. Each click reveals
 *  this many more folders AND this many more documents. Backend clamps the
 *  final value to the range [1, 100]. */
const FOLDER_CONTENT_PAGE_SIZE = 20;
const PROJECT_DOCUMENTS_PREFERENCE_PREFIX = 'knowledge-vault.project-documents.';

interface ProjectDocumentsPreference {
  defaultProjectId: string | null;
  lastProjectId: string | null;
  lastBrowseFolderId: string | null;
}

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
  private readonly auth = inject(AuthService);
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

  /** True while a "Load more" page is being fetched. Distinct from
   *  <see cref="loading"/> which only tracks the first page of a fresh
   *  browse so we can keep existing tiles visible while loading the next
   *  page (instead of clearing the canvas on every Load more). */
  readonly loadingMore = signal(false);

  /** True when the backend has at least one more folder page or one more
   *  document page to deliver. Drives the visibility of the "Load more"
   *  button. */
  readonly hasMoreContent = signal(false);

  readonly projectId = signal<string | null>(null);
  /** The folder being viewed in the regular, Explorer-like browser. */
  readonly browseFolderId = signal<string | null>(null);

  readonly hasFollowedProjects = computed(() => this.projects().length > 0);
  readonly noFollowedProjects = computed(() => this.isProjectScope && this.projects().length === 0);
  readonly canCreate = computed(() => !this.isProjectScope || this.hasFollowedProjects());

  /** Display name of the currently selected project. Resolved id-based so a
   *  project rename elsewhere stays consistent. Null when no project is
   *  selected or the project list has not loaded yet. Used by the browse
   *  breadcrumb to render the project segment between "Documents" and the
   *  folder trail. */
  readonly currentProjectName = computed(() => {
    const id = this.projectId();
    if (!id) {
      return null;
    }
    return this.projects().find((p) => p.id === id)?.name ?? null;
  });

  readonly editorOpen = signal(false);
  readonly selectedItem = signal<KnowledgeItem | null>(null);
  readonly selectedId = signal<string | null>(null);

  readonly createFolderOpen = signal(false);
  readonly createFolderName = signal('');
  readonly createFolderDescription = signal('');
  readonly createFolderProjectId = signal<string>('');

  /** Folder metadata cache keyed by id. Used by the browse-mode breadcrumb
   *  to resolve ancestor names. Id-based so it stays accurate after renames
   *  (we always re-fetch when a stale name is detected). */
  private readonly folderMetaById = signal<Map<string, FolderSummary>>(new Map());

  /** Breadcrumb trail from the workspace root down to the currently browsed
   *  folder in normal (non-workspace) mode. Empty when at the root or when
   *  no folder is being browsed. */
  readonly browseBreadcrumb = signal<Array<{ id: string; name: string }>>([]);

  /**
   * Display name of the folder a new sub-folder will be created in.
   * Resolved from the workspace tree by id so it stays correct even if the
   * folder is renamed elsewhere. Null when there is no resolvable parent
   * (e.g. before the tree has loaded).
   */
  readonly createFolderParentName = computed(() => {
    const state = this.workspace.current();
    const parentId = state ? state.currentFolderId : this.browseFolderId();
    return this.findFolderName(parentId);
  });

  /** Display name of the folder a new document will be saved into. Same id-driven
   *  resolution as the folder parent hint so it stays accurate after renames. */
  readonly createDocumentParentName = computed(() => {
    const state = this.workspace.current();
    const parentId = state ? state.currentFolderId : this.browseFolderId();
    return this.findFolderName(parentId);
  });

  /** Display name of the folder currently being explored in workspace mode.
   *  Drives the explorer header so the user always sees which folder's
   *  documents are listed. Null means the workspace root is being shown. */
  readonly currentFolderDisplayName = computed(() => {
    const state = this.workspace.current();
    if (!state) {
      return null;
    }
    if (state.currentFolderId === state.workspaceRootFolderId) {
      return this.findFolderName(state.workspaceRootFolderId);
    }
    return this.findFolderName(state.currentFolderId);
  });

  readonly renameFolderOpen = signal(false);
  readonly renameFolderId = signal<string | null>(null);
  readonly renameFolderName = signal('');

  readonly deleteFolderId = signal<string | null>(null);
  readonly showArchived = signal(false);

  readonly breadcrumbDropTargetId = signal<string | null>(null);
  readonly explorerDropTargetId = signal<string | null>(null);

  readonly activeDocument = signal<KnowledgeItem | null>(null);
  readonly activeDocumentLoading = signal(false);
  readonly activeDocumentError = signal<string | null>(null);

  private readonly sub = new Subscription();
  private lastProcessedMoveRequestId = 0;
  private preferenceResolved = false;

  constructor() {
    this.sub.add(
      this.route.queryParamMap.subscribe((params) => {
        const projectId = params.get('projectId');
        const rootFolderId = params.get('workspaceRootFolderId');
        const folderId = params.get('folderId');
        const browseFolderId = params.get('browseFolderId');

        if (projectId !== this.projectId()) {
          this.projectId.set(projectId);
        }

        if (this.isProjectScope && projectId) {
          this.saveProjectDocumentsPreference({
            lastProjectId: projectId,
            lastBrowseFolderId: browseFolderId,
          });
        }

        if (rootFolderId || folderId) {
          const root = rootFolderId ?? folderId;
          this.workspace.enterWorkspace({
            scope: this.workspaceScope,
            projectId: projectId ?? null,
            workspaceRootFolderId: root,
            currentFolderId: folderId ?? root,
          });
        } else {
          // A project root (and every normal folder browse) must keep the
          // standard application shell. Workspace mode is opt-in only.
          if (this.workspace.isWorkspaceMode()) {
            this.workspace.exitWorkspace();
          }
          this.browseFolderId.set(browseFolderId);
        }
      }),
    );

    effect(() => {
      const projectId = this.projectId();
      const state = this.workspace.current();
      const currentFolderId = this.workspace.currentFolderId();
      void projectId;
      void currentFolderId;
      // Always load the first page of content for the current view. The
      // grouped "All followed projects" view was removed; the project scope
      // root now displays the same single-folder tile grid scoped to the
      // selected project, with a "Pick a project" empty state when no
      // project is chosen.
      this.loadContent(state, 1, /*append*/ false);
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

    effect(() => {
      const moveRequest = this.workspace.documentMoveRequest();
      if (!moveRequest || moveRequest.requestId <= this.lastProcessedMoveRequestId) {
        return;
      }
      this.lastProcessedMoveRequestId = moveRequest.requestId;
      this.moveDocumentToFolder(moveRequest.documentId, moveRequest.folderId);
      this.workspace.clearDocumentMoveRequest();
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
        this.restoreProjectPreference(projects.items);
      },
      error: () => {
        /* reference data is non-critical */
      },
    });
  }

  private loadContent(state: WorkspaceState | null, page: number, append: boolean): void {
    if (this.isProjectScope && !this.projectId() && !state) {
      // Project scope with no project selected: clear everything and bail
      // so the empty state ("Pick a project") can render.
      this.loading.set(false);
      this.loadingMore.set(false);
      this.folders.set([]);
      this.documents.set([]);
      this.hasMoreContent.set(false);
      this.workspace.setTree(null);
      this.workspace.setBreadcrumb([]);
      this.workspace.setCurrentFolderDocuments(null, []);
      this.browseBreadcrumb.set([]);
      return;
    }

    if (append) {
      this.loadingMore.set(true);
    } else {
      this.loading.set(true);
    }
    this.error.set(null);

    const inWorkspace = state !== null;
    const parentFolderId = inWorkspace ? state!.currentFolderId : this.browseFolderId();
    const rootFolderId = inWorkspace ? state!.workspaceRootFolderId : null;

    const content$ = this.api.listFolderContent({
      scope: this.workspaceScope,
      projectId: this.projectId() ?? null,
      parentFolderId,
      rootFolderId,
      page,
      pageSize: FOLDER_CONTENT_PAGE_SIZE,
      includeArchived: inWorkspace || this.showArchived(),
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
        // Backend returns either a FolderContent (unpaged) or a
        // FolderContentPage (when page is supplied). We always supply a
        // page here, so narrow to the paged shape.
        const pageResult = content as import('../../../core/models/folder.models').FolderContentPage;
        const newFolders = pageResult.folders ?? [];
        const newDocuments = pageResult.documents ?? [];

        if (append) {
          // Append mode: dedupe by id (defensive) and concatenate.
          this.folders.update((existing) => dedupeAppend(existing, newFolders));
          this.documents.update((existing) => dedupeAppend(existing, newDocuments));
        } else {
          this.folders.set(newFolders);
          this.documents.set(newDocuments);
        }

        this.workspace.setCurrentFolderDocuments(inWorkspace ? state!.currentFolderId : null, inWorkspace ? newDocuments : []);
        this.workspace.setTree(tree);
        if (tree) {
          this.workspace.setBreadcrumb(this.buildPath(tree, state!.currentFolderId!));
        } else {
          this.workspace.setBreadcrumb([]);
        }
        // Normal (non-workspace) browse mode needs its own breadcrumb built
        // from the folder ancestors since there is no full tree available.
        if (!inWorkspace && parentFolderId) {
          this.loadBrowseBreadcrumb(parentFolderId);
        } else {
          this.browseBreadcrumb.set([]);
        }

        this.hasMoreContent.set(Boolean(pageResult.hasMore));
        this.lastLoadedPage = pageResult.page;
      },
      error: (err: unknown) => this.error.set(getErrorMessage(err)),
      complete: () => {
        this.loading.set(false);
        this.loadingMore.set(false);
      },
    });
  }

  /** Track the last page that was successfully merged into the visible
   *  folder/document lists so "Load more" can request the next one. */
  private lastLoadedPage = 0;

  /**
   * Fetch the next page of folders/documents and append it to the visible
   * lists. Disabled while a page is already in flight or when the backend
   * has reported no more data.
   */
  loadMore(): void {
    if (this.loadingMore() || !this.hasMoreContent() || this.loading()) {
      return;
    }
    this.loadContent(this.workspace.current(), this.lastLoadedPage + 1, /*append*/ true);
  }

  private restoreProjectPreference(projects: ProjectSummary[]): void {
    if (!this.isProjectScope || this.projectId() || this.preferenceResolved) {
      return;
    }

    this.preferenceResolved = true;
    const preference = this.readProjectDocumentsPreference();
    const availableIds = new Set(projects.map((project) => project.id));
    const projectId = [preference.lastProjectId, preference.defaultProjectId]
      .find((id): id is string => !!id && availableIds.has(id));

    if (!projectId) {
      return;
    }

    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        projectId,
        browseFolderId: projectId === preference.lastProjectId ? preference.lastBrowseFolderId : null,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private readProjectDocumentsPreference(): ProjectDocumentsPreference {
    const fallback: ProjectDocumentsPreference = {
      defaultProjectId: null,
      lastProjectId: null,
      lastBrowseFolderId: null,
    };
    if (typeof localStorage === 'undefined') {
      return fallback;
    }

    try {
      const value = localStorage.getItem(this.projectDocumentsPreferenceKey());
      return value ? { ...fallback, ...(JSON.parse(value) as Partial<ProjectDocumentsPreference>) } : fallback;
    } catch {
      return fallback;
    }
  }

  private saveProjectDocumentsPreference(change: Partial<ProjectDocumentsPreference>): void {
    if (typeof localStorage === 'undefined') {
      return;
    }

    const preference = { ...this.readProjectDocumentsPreference(), ...change };
    localStorage.setItem(this.projectDocumentsPreferenceKey(), JSON.stringify(preference));
  }

  private projectDocumentsPreferenceKey(): string {
    return `${PROJECT_DOCUMENTS_PREFERENCE_PREFIX}${this.auth.currentUser()?.id ?? 'anonymous'}`;
  }

  toggleShowArchived(): void {
    this.showArchived.update(value => !value);
    this.loadContent(this.workspace.current(), 1, false);
  }

  /**
   * Resolve the ancestor chain for the currently browsed folder (normal
   * browse mode). Each step uses `getFolder(id)` which returns the folder's
   * `parentFolderId` so we can walk up to the root. We stop as soon as we
   * reach a folder with no parent (root) or after a defensive max-depth
   * safety cap. The resulting trail is root → leaf order, ready for the
   * breadcrumb UI to render.
   */
  private loadBrowseBreadcrumb(folderId: string): void {
    const maxDepth = 32;
    const chain: FolderSummary[] = [];
    let nextId: string | null = folderId;

    const finish = (): void => {
      chain.reverse();
      this.browseBreadcrumb.set(chain.map((f) => ({ id: f.id, name: f.name })));
    };

    const visit = (depth: number): void => {
      if (!nextId || depth > maxDepth) {
        finish();
        return;
      }
      const cached = this.folderMetaById().get(nextId);
      if (cached) {
        chain.push(cached);
        nextId = cached.parentFolderId ?? null;
        visit(depth + 1);
        return;
      }
      this.api.getFolder(nextId).subscribe({
        next: (folder) => {
          this.folderMetaById.update((map) => {
            const next = new Map(map);
            next.set(folder.id, folder);
            return next;
          });
          chain.push(folder);
          nextId = folder.parentFolderId ?? null;
          visit(depth + 1);
        },
        error: () => finish(),
      });
    };

    visit(0);
  }

  /**
   * Clear the breadcrumb trail and folder metadata cache when navigating to
   * a different folder/scope so stale names from a previous project don't
   * bleed into the new view.
   */
  private resetBrowseBreadcrumb(): void {
    this.browseBreadcrumb.set([]);
    this.folderMetaById.set(new Map());
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

  /**
   * Click handler for a breadcrumb segment in the normal browse mode. The
   * breadcrumb carries the segment id, so navigating by id means we stay
   * correct even if the folder was renamed elsewhere.
   */
  navigateBreadcrumb(folderId: string | null): void {
    if (folderId === null) {
      this.browseRoot();
      return;
    }
    if (this.workspace.isWorkspaceMode()) {
      // Shouldn't happen because the breadcrumb only renders outside
      // workspace mode, but be defensive: in workspace mode the breadcrumb
      // click must update currentFolderId, not just the URL.
      const state = this.workspace.current();
      if (state) {
        this.workspace.enterWorkspace({ ...state, currentFolderId: folderId });
      }
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { browseFolderId: folderId },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private findFolder(id: string): FolderSummary | undefined {
    return this.folders().find((f) => f.id === id);
  }

  /**
   * Resolve a folder's display name by id, looking through the current page's
   * folders, the workspace tree, and the breadcrumb path. Returns null when
   * the id cannot be resolved yet. Id-based so it stays accurate across
   * renames.
   */
  private findFolderName(id: string | null): string | null {
    if (!id) {
      return null;
    }
    const flat = this.findFolder(id);
    if (flat) {
      return flat.name;
    }
    const tree = this.workspace.folderTree();
    if (tree) {
      const fromTree = this.findInTree(tree, id);
      if (fromTree) {
        return fromTree;
      }
    }
    const crumb = this.workspace.breadcrumb().find((n) => n.id === id);
    if (crumb) {
      return crumb.name;
    }
    // Browse-mode metadata cache (populated by the breadcrumb walk). Lets the
    // "New folder / New document" dialog show the currently browsed folder
    // even when it is not visible in the current page's folder list (its
    // own children are shown instead, not itself).
    const cached = this.folderMetaById().get(id);
    if (cached) {
      return cached.name;
    }
    return null;
  }

  private findInTree(node: FolderTreeNode, id: string): string | null {
    if (node.id === id) {
      return node.name;
    }
    for (const child of node.children) {
      const hit = this.findInTree(child, id);
      if (hit !== null) {
        return hit;
      }
    }
    return null;
  }

  // ----- Navigation -----
  openFolder(folderId: string): void {
    if (this.workspace.isWorkspaceMode()) {
      this.workspace.setCurrentFolder(folderId);
    } else {
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { browseFolderId: folderId },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
    }
  }

  openWorkspace(folderId: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        browseFolderId: null,
        workspaceRootFolderId: folderId,
        folderId,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
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
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { workspaceRootFolderId: null, folderId: null, browseFolderId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  browseRoot(): void {
    this.resetBrowseBreadcrumb();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { browseFolderId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  /**
   * Return to the project library root with the current project preselected
   * in the project dropdown. Triggered by clicking the project segment in
   * the browse-mode breadcrumb. Clears the browse folder so the grouped
   * project view (or the single-project list) renders again instead of the
   * drilled-in folder contents.
   */
  goToProject(): void {
    const projectId = this.projectId();
    this.resetBrowseBreadcrumb();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        projectId,
        browseFolderId: null,
        workspaceRootFolderId: null,
        folderId: null,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  onProjectChange(projectId: string): void {
    this.preferenceResolved = true;
    this.resetBrowseBreadcrumb();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        projectId: projectId || null,
        browseFolderId: null,
        workspaceRootFolderId: null,
        folderId: null,
      },
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
        parentFolderId: state ? state.currentFolderId : this.browseFolderId(),
        name,
        description: this.createFolderDescription().trim() || null,
      })
      .subscribe({
        next: () => {
          this.createFolderOpen.set(false);
          this.saving.set(false);
          this.loadContent(this.workspace.current(), 1, /*append*/ false);
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
        this.loadContent(this.workspace.current(), 1, /*append*/ false);
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
    this.api.archiveFolder(id).subscribe({
      next: () => {
        this.deleteFolderId.set(null);
        this.saving.set(false);
        const state = this.workspace.current();
        if (state && state.workspaceRootFolderId === id) {
          this.exitWorkspace();
        } else if (state && state.currentFolderId === id) {
          this.workspace.setCurrentFolder(state.workspaceRootFolderId);
        } else {
          this.loadContent(state, 1, /*append*/ false);
        }
      },
      error: (err: unknown) => {
        const message = getErrorMessage(err);
        this.error.set(
          message.includes('409')
            ? 'The folder could not be archived.'
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

  /** Open a folder from the explorer list (workspace mode) — equivalent to
   *  clicking the folder in the sidebar tree. */
  openFolderFromExplorer(folderId: string): void {
    if (this.workspace.isWorkspaceMode()) {
      this.workspace.setCurrentFolder(folderId);
    }
  }

  /** Open a document from the explorer list (workspace mode) — opens the
   *  document in a new tab (or activates its existing tab). */
  openDocumentFromExplorer(doc: KnowledgeItemSummary): void {
    this.openDocument(doc, doc.title);
  }

  moveDocumentToFolder(documentId: string, folderId: string | null): void {
    this.saving.set(true);
    this.api.moveDocument(documentId, folderId).subscribe({
      next: () => {
        this.saving.set(false);
        this.loadContent(this.workspace.current(), 1, /*append*/ false);
      },
      error: (err: unknown) => {
        this.error.set(getErrorMessage(err));
        this.saving.set(false);
      },
    });
  }

  downloadDocument(documentId: string): void {
    const title = this.findDocumentTitle(documentId) ?? 'document';
    this.api.downloadDocument(documentId).subscribe({
      next: (blob) => this.triggerBrowserDownload(blob, this.documentFileName(title)),
      error: (err: unknown) => this.error.set(getErrorMessage(err)),
    });
  }

  downloadFolder(folderId: string): void {
    const name = this.findFolderName(folderId) ?? 'folder';
    this.api.downloadFolder(folderId).subscribe({
      next: (blob) => this.triggerBrowserDownload(blob, `${this.sanitizeFileName(name)}.zip`),
      error: (err: unknown) => this.error.set(getErrorMessage(err)),
    });
  }

  onBreadcrumbDragOver(event: DragEvent): void {
    if (!event.dataTransfer) {
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }

  onBreadcrumbDragEnter(event: DragEvent, folderId: string | null): void {
    event.preventDefault();
    this.breadcrumbDropTargetId.set(folderId ?? 'root');
  }

  onBreadcrumbDragLeave(_event: DragEvent, folderId: string | null): void {
    const key = folderId ?? 'root';
    if (this.breadcrumbDropTargetId() === key) {
      this.breadcrumbDropTargetId.set(null);
    }
  }

  onBreadcrumbDrop(event: DragEvent, folderId: string | null): void {
    event.preventDefault();
    this.breadcrumbDropTargetId.set(null);
    const documentId = event.dataTransfer?.getData('application/x-kv-document-id')
      ?? event.dataTransfer?.getData('text/plain')
      ?? '';
    if (!documentId) {
      return;
    }
    this.moveDocumentToFolder(documentId, folderId);
  }

  onExplorerFolderDragOver(event: DragEvent): void {
    if (!event.dataTransfer) {
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }

  onExplorerFolderDragEnter(event: DragEvent, folderId: string): void {
    event.preventDefault();
    this.explorerDropTargetId.set(folderId);
  }

  onExplorerFolderDragLeave(_event: DragEvent, folderId: string): void {
    if (this.explorerDropTargetId() === folderId) {
      this.explorerDropTargetId.set(null);
    }
  }

  onExplorerFolderDrop(event: DragEvent, folderId: string): void {
    event.preventDefault();
    this.explorerDropTargetId.set(null);
    const documentId = event.dataTransfer?.getData('application/x-kv-document-id')
      ?? event.dataTransfer?.getData('text/plain')
      ?? '';
    if (!documentId) {
      return;
    }
    this.moveDocumentToFolder(documentId, folderId);
  }

  private findDocumentTitle(id: string): string | null {
    return this.documents().find((doc) => doc.id === id)?.title ?? null;
  }

  private sanitizeFileName(name: string): string {
    const sanitized = name.replace(/[\\/:*?"<>|]/g, '_').trim();
    return sanitized || 'download';
  }

  private documentFileName(title: string): string {
    const fileName = this.sanitizeFileName(title);
    return /\.[^./\\\s]+$/.test(fileName) ? fileName : `${fileName}.md`;
  }

  private triggerBrowserDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  deleteDocument(id: string): void {
    if (!confirm('Archive this document? You can show archived items in browse mode.')) {
      return;
    }
    this.saving.set(true);
    this.api.archiveKnowledgeItem(id).subscribe({
      next: () => {
        this.saving.set(false);
        this.loadContent(this.workspace.current(), 1, /*append*/ false);
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
    const folderId = item ? undefined : state ? state.currentFolderId : this.browseFolderId();
    const payload: SaveDocumentRequest = { ...request, folderId };
    const operation = item
      ? this.api.updateKnowledgeItem(item.id, payload)
      : this.api.createKnowledgeItem(payload);

    operation.subscribe({
      next: () => {
        this.editorOpen.set(false);
        this.saving.set(false);
        this.loadContent(this.workspace.current(), 1, /*append*/ false);
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

/**
 * Concatenate <paramref name="incoming"/> onto <paramref name="existing"/>
 * while removing duplicates by id. Backend ordering is authoritative, so
 * appended items follow whatever order the server returned. The id-based
 * dedupe is defensive: in normal flow the same id cannot appear on two
 * pages, but if a rename or background mutation causes overlap, the
 * newest occurrence wins (i.e. later in <paramref name="incoming"/>).
 */
function dedupeAppend<T extends { id: string }>(existing: T[], incoming: T[]): T[] {
  if (incoming.length === 0) {
    return existing;
  }
  const seen = new Set(existing.map((item) => item.id));
  const merged = [...existing];
  for (const item of incoming) {
    if (seen.has(item.id)) {
      continue;
    }
    seen.add(item.id);
    merged.push(item);
  }
  return merged;
}
