import { Component, HostListener, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ApiClient } from '../../../core/api/api-client.service';
import { AuthService } from '../../../core/auth/auth.service';
import { getErrorMessage } from '../../../core/http/error-message';
import {
  AVAILABLE_SCOPES,
  ApiKey,
  ApiKeyCreated,
  UserProfile,
} from '../../../core/models/profile.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

@Component({
  selector: 'app-profile-page',
  imports: [DatePipe, ReactiveFormsModule, LoadingIndicator],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.css',
})
export class ProfilePage {
  private readonly api = inject(ApiClient);
  private readonly authService = inject(AuthService);
  private readonly fb = new FormBuilder();

  readonly availableScopes = AVAILABLE_SCOPES;
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<UserProfile | null>(null);
  readonly apiKeys = signal<ApiKey[]>([]);
  readonly createdKey = signal<ApiKeyCreated | null>(null);
  readonly createDialogOpen = signal(false);
  readonly keyError = signal<string | null>(null);
  readonly creating = signal(false);
  readonly savingProfile = signal(false);
  readonly profileSaved = signal(false);
  readonly copied = signal(false);

  readonly nicknameForm = this.fb.nonNullable.group({
    nickname: [''],
  });

  readonly keyForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(64)]],
    expiresInDays: [365, [Validators.min(1), Validators.max(365)]],
    scopes: this.fb.nonNullable.control<string[]>([], [Validators.required, Validators.minLength(1)]),
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getProfile().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.nicknameForm.controls.nickname.setValue(profile.nickname ?? '');
        this.loadKeys();
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  loadKeys(): void {
    this.api.listApiKeys().subscribe({
      next: (keys) => this.apiKeys.set(keys),
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  saveNickname(): void {
    this.savingProfile.set(true);
    this.profileSaved.set(false);
    const nickname = this.nicknameForm.controls.nickname.value.trim() || null;
    this.api.updateProfile(nickname).subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.profileSaved.set(true);
        this.authService.refreshMe().subscribe();
      },
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.savingProfile.set(false),
    });
  }

  toggleScope(value: string, checked: boolean): void {
    const current = this.keyForm.controls.scopes.value;
    const next = checked ? [...current, value] : current.filter((s) => s !== value);
    this.keyForm.controls.scopes.setValue(next);
    this.keyForm.controls.scopes.markAsDirty();
  }

  openCreateDialog(): void {
    this.keyError.set(null);
    this.keyForm.reset({ name: '', expiresInDays: 365, scopes: [] });
    this.createDialogOpen.set(true);
  }

  closeCreateDialog(): void {
    if (this.creating()) {
      return;
    }

    this.createDialogOpen.set(false);
    this.keyError.set(null);
    this.keyForm.reset({ name: '', expiresInDays: 365, scopes: [] });
  }

  @HostListener('document:keydown.escape')
  closeCreateDialogOnEscape(): void {
    if (this.createDialogOpen()) {
      this.closeCreateDialog();
    }
  }

  createKey(): void {
    if (this.keyForm.invalid) {
      this.keyForm.markAllAsTouched();
      return;
    }

    this.creating.set(true);
    this.keyError.set(null);
    const value = this.keyForm.getRawValue();
    this.api
      .createApiKey({
        name: value.name,
        scopes: value.scopes,
        expiresInDays: value.expiresInDays,
      })
      .subscribe({
        next: (created) => {
          this.createdKey.set(created);
          this.createDialogOpen.set(false);
          this.keyForm.reset({ name: '', expiresInDays: 365, scopes: [] });
          this.loadKeys();
        },
        error: (error) => this.keyError.set(getErrorMessage(error)),
        complete: () => this.creating.set(false),
      });
  }

  keyStatus(key: ApiKey): 'Active' | 'Expired' | 'Revoked' {
    if (key.isRevoked) {
      return 'Revoked';
    }

    if (key.expiresAt && new Date(key.expiresAt).getTime() <= Date.now()) {
      return 'Expired';
    }

    return 'Active';
  }

  revokeKey(id: string): void {
    this.api.revokeApiKey(id).subscribe({
      next: () => this.loadKeys(),
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }

  dismissCreatedKey(): void {
    this.createdKey.set(null);
    this.copied.set(false);
  }

  async copyCreatedKey(): Promise<void> {
    const key = this.createdKey()?.key;
    if (!key || typeof navigator === 'undefined' || !navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(key);
    this.copied.set(true);
  }
}
