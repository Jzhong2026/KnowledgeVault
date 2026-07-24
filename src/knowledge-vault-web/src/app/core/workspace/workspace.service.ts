import { Injectable, Signal, computed, signal } from '@angular/core';

import { DocumentScope } from '../models/knowledge.models';
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

const STORAGE_PREFIX = 'kv:workspace:';

@Injectable({ providedIn: 'root' })
export class WorkspaceService {
  private readonly state = signal<WorkspaceState | null>(null);
  private readonly tree = signal<FolderTreeNode | null>(null);
  private readonly breadcrumbPath = signal<BreadcrumbNode[]>([]);
  private readonly tabs = signal<OpenTab[]>([]);
  private readonly activeTabId = signal<string | null>(null);

  readonly isWorkspaceMode = computed(() => this.state() !== null);
  readonly current = this.state.asReadonly();
  readonly workspaceRootFolderId = computed(() => this.state()?.workspaceRootFolderId ?? null);
  readonly currentFolderId = computed(() => this.state()?.currentFolderId ?? null);
  readonly scope = computed(() => this.state()?.scope ?? null);
  readonly projectId = computed(() => this.state()?.projectId ?? null);
  readonly folderTree = this.tree.asReadonly();
  readonly breadcrumb = this.breadcrumbPath.asReadonly();
  readonly openTabs = this.tabs.asReadonly();
  readonly activeTabIdSignal = this.activeTabId.asReadonly();
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
    this.persist(updated);
  }

  exitWorkspace(): void {
    const current = this.state();
    this.state.set(null);
    this.tree.set(null);
    this.breadcrumbPath.set([]);
    this.tabs.set([]);
    this.activeTabId.set(null);
    if (current) {
      this.clear(current);
    }
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
  }

  setBreadcrumb(path: BreadcrumbNode[]): void {
    this.breadcrumbPath.set(path);
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
