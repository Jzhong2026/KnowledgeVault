import { Component, computed, HostListener, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-topbar',
  imports: [RouterLink],
  templateUrl: './topbar.html',
  styleUrl: './topbar.css',
})
export class Topbar {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.authService.currentUser;
  readonly displayName = computed(() => this.user()?.nickname?.trim() || this.user()?.userName || 'Profile');
  readonly initials = computed(() => this.displayName().slice(0, 2).toUpperCase());
  readonly menuOpen = signal(false);

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

  logout(): void {
    this.closeMenu();
    this.authService.logout();
    void this.router.navigate(['/auth']);
  }
}
