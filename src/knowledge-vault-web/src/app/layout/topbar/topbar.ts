import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { Breadcrumb } from '../workspace/breadcrumb/breadcrumb';

@Component({
  selector: 'app-topbar',
  imports: [RouterLink, Breadcrumb],
  templateUrl: './topbar.html',
  styleUrl: './topbar.css',
})
export class Topbar {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly workspace = inject(WorkspaceService);

  readonly user = this.authService.currentUser;
  readonly displayName = computed(() => this.user()?.nickname?.trim() || this.user()?.userName || 'Profile');
  readonly initials = computed(() => this.displayName().slice(0, 2).toUpperCase());
  readonly menuOpen = signal(false);

  readonly isWorkspaceMode = this.workspace.isWorkspaceMode;
  readonly breadcrumbPath = this.workspace.breadcrumb;

  toggleMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.menuOpen.update((isOpen) => !isOpen);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.closeMenu();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenu();
  }

  exitWorkspace(): void {
    this.workspace.exitWorkspace();
    void this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  onBreadcrumbNavigate(folderId: string): void {
    this.workspace.setCurrentFolder(folderId);
  }

  logout(): void {
    this.closeMenu();
    this.authService.logout();
    void this.router.navigate(['/auth']);
  }
}
