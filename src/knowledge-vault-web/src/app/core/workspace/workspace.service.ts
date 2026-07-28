import { Injectable, Signal, computed, signal } from '@angular/core';

import { DocumentScope } from '../models/knowledge.models';
import { KnowledgeItemSummary } from '../models/knowledge.models';
import { FolderTreeNode } from '../models/folder.models';

export interface WorkspaceState {
  scope: DocumentScope;
  projectId: string | null;
  workspaceRootFolderId: string | null;
  currentFolderId: string | null;
}

export interface BreadcrumbNode {
  id: string;
  name: string;
}

export interface OpenTab {
  id: string;          // tab identifier (uuid)
  documentId: string;
  title: string;
}

export interface DocumentMoveRequest {
  documentId: string;
  folderId: string | null;
  requestId: number;
}

const STORAGE_PREFIX = 'kv:workspace:';

@Injectable({ providedIn: 'root' })
export class WorkspaceService {
  private readonly state = signal<WorkspaceState | null>(null);
  private readonly tree = signal<FolderTreeNode | null>(null);
  private readonly breadcrumbPath = signal<BreadcrumbNode[]>([]);
  private readonly currentFolderDocs = signal<KnowledgeItemSummary[]>([]);
  private readonly folderDocuments = signal<ReadonlyMap<string, KnowledgeItemSummary[]>>(new Map());
  private readonly tabs = signal<OpenTab[]>([]);
  private readonly activeTabId = signal<string | null>(null);
  private readonly moveRequest = signal<DocumentMoveRequest | null>(null);
  /** Folder ids the user collapsed in the sidebar tree. Kept in the shared
   *  service (instead of per-component) so the collapse state survives tree
   *  reloads that happen when switching the active folder or refreshing data. */
  private readonly collapsedNodeIds = signal<Set<string>>(new Set());

  readonly isWorkspaceMode = computed(() => this.state() !== null);
  readonly current = this.state.asReadonly();
  readonly workspaceRootFolderId = computed(() => this.state()?.workspaceRootFolderId ?? null);
  readonly currentFolderId = computed(() => this.state()?.currentFolderId ?? null);
  readonly scope = computed(() => this.state()?.scope ?? null);
  readonly projectId = computed(() => this.state()?.projectId ?? null);
  readonly folderTree = this.tree.asReadonly();
  readonly breadcrumb = this.breadcrumbPath.asReadonly();
  readonly currentFolderDocuments = this.currentFolderDocs.asReadonly();
  readonly folderDocumentsById = this.folderDocuments.asReadonly();
  /** Display name of the current workspace root. Derived from the loaded tree. */
  readonly rootName = computed(() => this.tree()?.name ?? null);
  readonly openTabs = this.tabs.asReadonly();
  readonly activeTabIdSignal = this.activeTabId.asReadonly();
  readonly documentMoveRequest = this.moveRequest.asReadonly();
  readonly collapsedNodeIdsSignal = this.collapsedNodeIds.asReadonly();
  readonly activeTab = computed(() => {
    const id = this.activeTabId();
    if (!id) return null;
    return this.tabs().find((t) => t.id === id) ?? null;
  });

  enterWorkspace(next: WorkspaceState): void {
    this.state.set(next);
    this.persist(next);
  }

  setCurrentFolder(folderId: string | null): void {
    const current = this.state();
    if (!current) {
      return;
    }
    if (folderId !== null && !this.isWithinRoot(this.tree(), folderId)) {
      return;
    }
    const updated: WorkspaceState = { ...current, currentFolderId: folderId };
    this.state.set(updated);
    this.expandPathTo(this.tree(), folderId);
    this.persist(updated);
  }

  exitWorkspace(): void {
    const current = this.state();
    this.state.set(null);
    this.tree.set(null);
    this.breadcrumbPath.set([]);
    this.currentFolderDocs.set([]);
    this.folderDocuments.set(new Map());
    this.tabs.set([]);
    this.activeTabId.set(null);
    this.collapsedNodeIds.set(new Set());
    if (current) {
      this.clear(current);
    }
  }

  requestDocumentMove(documentId: string, folderId: string | null): void {
    this.moveRequest.set({
      documentId,
      folderId,
      requestId: Date.now(),
    });
  }

  clearDocumentMoveRequest(): void {
    this.moveRequest.set(null);
  }

  /** Whether a folder in the sidebar tree is currently collapsed. */
  isNodeCollapsed(folderId: string): boolean {
    return this.collapsedNodeIds().has(folderId);
  }

  /** Toggle the collapsed state of a folder in the sidebar tree. */
  toggleCollapsedNode(folderId: string): void {
    this.collapsedNodeIds.update((set) => {
      const next = new Set(set);
      if (next.has(folderId)) {
        next.delete(folderId);
      } else {
        next.add(folderId);
      }
      return next;
    });
  }

  /** Open a document in a new tab (or activate an existing one for the same doc). */
  openDocumentTab(documentId: string, title: string): void {
    const existing = this.tabs().find((t) => t.documentId === documentId);
    if (existing) {
      this.activeTabId.set(existing.id);
      return;
    }
    const tab: OpenTab = {
      id: crypto.randomUUID ? crypto.randomUUID() : `${documentId}-${Date.now()}`,
      documentId,
      title: title || 'Untitled',
    };
    this.tabs.update((list) => [...list, tab]);
    this.activeTabId.set(tab.id);
  }

  activateTab(id: string): void {
    if (this.tabs().some((t) => t.id === id)) {
      this.activeTabId.set(id);
    }
  }

  closeTab(id: string): void {
    const remaining = this.tabs().filter((t) => t.id !== id);
    this.tabs.set(remaining);
    if (this.activeTabId() === id) {
      this.activeTabId.set(remaining.length > 0 ? remaining[remaining.length - 1].id : null);
    }
  }

  restore(scope: DocumentScope, projectId: string | null): WorkspaceState | null {
    const raw = this.readStorage(scope, projectId);
    if (!raw) {
      this.state.set(null);
      return null;
    }
    try {
      const parsed = JSON.parse(raw) as WorkspaceState;
      this.state.set(parsed);
      return parsed;
    } catch {
      this.state.set(null);
      return null;
    }
  }

  setTree(next: FolderTreeNode | null): void {
    this.tree.set(next);
    this.expandPathTo(next, this.state()?.currentFolderId ?? null);
  }

  setBreadcrumb(path: BreadcrumbNode[]): void {
    this.breadcrumbPath.set(path);
  }

  setCurrentFolderDocuments(folderId: string | null, docs: KnowledgeItemSummary[]): void {
    this.currentFolderDocs.set(docs);
    if (folderId) {
      this.folderDocuments.update(existing => {
        const next = new Map(existing);
        next.set(folderId, docs);
        return next;
      });
    }
  }

  private isWithinRoot(node: FolderTreeNode | null, targetId: string): boolean {
    if (!node) {
      return false;
    }
    if (node.id === targetId) {
      return true;
    }
    return node.children.some((child) => this.isWithinRoot(child, targetId));
  }

  /**
   * Keep the selected folder visible in the sidebar by expanding every
   * ancestor from the workspace root to that folder. This is invoked both
   * when a folder is selected and when a freshly loaded tree arrives.
   */
  private expandPathTo(node: FolderTreeNode | null, targetId: string | null): void {
    if (!node || !targetId) {
      return;
    }

    const path: string[] = [];
    const findPath = (current: FolderTreeNode): boolean => {
      path.push(current.id);
      if (current.id === targetId) {
        return true;
      }
      for (const child of current.children) {
        if (findPath(child)) {
          return true;
        }
      }
      path.pop();
      return false;
    };

    if (!findPath(node)) {
      return;
    }

    this.collapsedNodeIds.update((collapsed) => {
      const next = new Set(collapsed);
      for (const folderId of path) {
        next.delete(folderId);
      }
      return next;
    });
  }

  private keyFor(scope: DocumentScope, projectId: string | null): string {
    return `${STORAGE_PREFIX}${scope}:${projectId ?? ''}`;
  }

  private persist(state: WorkspaceState): void {
    try {
      localStorage.setItem(this.keyFor(state.scope, state.projectId), JSON.stringify(state));
    } catch {
      // ignore storage failures (private mode, quota, etc.)
    }
  }

  private readStorage(scope: DocumentScope, projectId: string | null): string | null {
    try {
      return localStorage.getItem(this.keyFor(scope, projectId));
    } catch {
      return null;
    }
  }

  private clear(state: WorkspaceState): void {
    try {
      localStorage.removeItem(this.keyFor(state.scope, state.projectId));
    } catch {
      // ignore
    }
  }
}
