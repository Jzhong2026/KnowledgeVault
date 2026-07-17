import { Component, computed, inject } from '@angular/core';
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

  logout(): void {
    this.authService.logout();
    void this.router.navigate(['/auth']);
  }
}
