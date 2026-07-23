import { Component, computed, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { WorkspaceService } from '../../core/workspace/workspace.service';
import { Sidebar } from '../sidebar/sidebar';
import { Topbar } from '../topbar/topbar';
import { WorkspaceMode } from '../workspace/workspace-mode/workspace-mode';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, Sidebar, Topbar, WorkspaceMode],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css',
})
export class AppShell {
  private readonly workspace = inject(WorkspaceService);
  readonly sidebarCollapsed = signal(false);
  readonly isWorkspaceMode = this.workspace.isWorkspaceMode;
}
