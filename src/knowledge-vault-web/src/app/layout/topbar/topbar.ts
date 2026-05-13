import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-topbar',
  templateUrl: './topbar.html',
  styleUrl: './topbar.css',
})
export class Topbar {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.authService.currentUser;
  readonly initials = computed(() => this.user()?.userName.slice(0, 2).toUpperCase() ?? 'KV');

  logout(): void {
    this.authService.logout();
    void this.router.navigate(['/auth']);
  }
}
