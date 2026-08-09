import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { WorkspaceService } from '../../core/workspace/workspace.service';
import { Sidebar } from '../sidebar/sidebar';
import { Topbar } from '../topbar/topbar';
import { WorkspaceMode } from '../workspace/workspace-mode/workspace-mode';
import { ChatPanel } from '../../features/chat/chat-panel';

/** Storage key for the user's preferred sidebar width. Persisting across
 *  sessions so users don't have to re-drag every time. */
const SIDEBAR_WIDTH_STORAGE_KEY = 'kv.sidebar.width';

/** Min/max/default widths (px) for the left navigation panel. Min keeps
 *  brand + nav readable; max prevents the panel from dominating the
 *  viewport on wide screens. */
const SIDEBAR_WIDTH_MIN = 200;
const SIDEBAR_WIDTH_MAX = 520;
const SIDEBAR_WIDTH_DEFAULT = 264;

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, Sidebar, Topbar, WorkspaceMode, ChatPanel],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css',
})
export class AppShell {
  private readonly workspace = inject(WorkspaceService);
  readonly sidebarCollapsed = signal(false);
  readonly isWorkspaceMode = this.workspace.isWorkspaceMode;

  /** Live width of the left panel (sidebar or workspace-mode). Stored as a
   *  signal so the template's CSS variable binding reactively tracks
   *  drag/resize updates. */
  readonly sidebarWidth = signal<number>(this.loadStoredWidth());

  /** Inline style binding for the .shell grid-template-columns. Reads from
   *  the signal so it stays in sync with drags. The collapsed variant
   *  keeps its own fixed 82px width via the .shell--sidebar-collapsed
   *  class — no resizing applies in collapsed mode. */
  readonly gridColumns = computed(() => {
    if (this.sidebarCollapsed()) {
      return '82px minmax(0, 1fr)';
    }
    return `${this.sidebarWidth()}px minmax(0, 1fr)`;
  });

  /** True while the user is actively dragging the resizer. Used by the
   *  template to set a `cursor: col-resize` body class so the cursor
   *  stays correct even when the pointer leaves the handle. */
  readonly resizing = signal(false);

  startResize(event: MouseEvent): void {
    if (this.sidebarCollapsed()) {
      return;
    }
    event.preventDefault();
    this.resizing.set(true);
  }

  resetWidth(event: MouseEvent): void {
    // Double-click on the handle resets to the default width so users have
    // an escape hatch if they drag the panel into an awkward size.
    if (event.detail < 2) {
      return;
    }
    this.applyWidth(SIDEBAR_WIDTH_DEFAULT);
  }

  @HostListener('document:mousemove', ['$event'])
  onDocumentMouseMove(event: MouseEvent): void {
    if (!this.resizing()) {
      return;
    }
    const next = Math.min(
      SIDEBAR_WIDTH_MAX,
      Math.max(SIDEBAR_WIDTH_MIN, event.clientX),
    );
    if (next !== this.sidebarWidth()) {
      this.sidebarWidth.set(next);
    }
  }

  @HostListener('document:mouseup')
  onDocumentMouseUp(): void {
    if (!this.resizing()) {
      return;
    }
    this.resizing.set(false);
    this.persistWidth(this.sidebarWidth());
  }

  private applyWidth(width: number): void {
    const clamped = Math.min(SIDEBAR_WIDTH_MAX, Math.max(SIDEBAR_WIDTH_MIN, width));
    this.sidebarWidth.set(clamped);
    this.persistWidth(clamped);
  }

  private loadStoredWidth(): number {
    if (typeof localStorage === 'undefined') {
      return SIDEBAR_WIDTH_DEFAULT;
    }
    const raw = localStorage.getItem(SIDEBAR_WIDTH_STORAGE_KEY);
    if (!raw) {
      return SIDEBAR_WIDTH_DEFAULT;
    }
    const parsed = Number.parseInt(raw, 10);
    if (!Number.isFinite(parsed)) {
      return SIDEBAR_WIDTH_DEFAULT;
    }
    return Math.min(SIDEBAR_WIDTH_MAX, Math.max(SIDEBAR_WIDTH_MIN, parsed));
  }

  private persistWidth(width: number): void {
    if (typeof localStorage === 'undefined') {
      return;
    }
    try {
      localStorage.setItem(SIDEBAR_WIDTH_STORAGE_KEY, String(width));
    } catch {
      // Quota / privacy mode: silently ignore — the in-memory signal still
      // holds the value for the current session.
    }
  }
}
