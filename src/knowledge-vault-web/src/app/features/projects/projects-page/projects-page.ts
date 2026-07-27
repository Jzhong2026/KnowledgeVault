import { DatePipe } from '@angular/common';
import { afterNextRender, Component, HostListener, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ApiClient } from '../../../core/api/api-client.service';
import { AuthService } from '../../../core/auth/auth.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { ProjectSummary } from '../../../core/models/projects.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

const PROJECT_DOCUMENTS_PREFERENCE_PREFIX = 'knowledge-vault.project-documents.';

interface ProjectDocumentsPreference {
  defaultProjectId: string | null;
  lastProjectId: string | null;
  lastBrowseFolderId: string | null;
}

@Component({
  selector: 'app-projects-page',
  imports: [DatePipe, FormsModule, ReactiveFormsModule, RouterLink, LoadingIndicator],
  templateUrl: './projects-page.html',
  styleUrl: './projects-page.css',
})
export class ProjectsPage {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly followBusyId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly projects = signal<ProjectSummary[]>([]);
  readonly showCreate = signal(false);
  readonly projectNameInput = viewChild.required<HTMLInputElement>('projectNameInput');
  readonly search = signal('');
  readonly followingOnly = signal(false);
  readonly defaultProjectId = signal<string | null>(this.readProjectDocumentsPreference().defaultProjectId);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    description: ['', [Validators.maxLength(512)]],
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .listProjects({
        search: this.search().trim() || undefined,
        followingOnly: this.followingOnly(),
        pageSize: 100,
      })
      .subscribe({
        next: (result) => this.projects.set(result.items),
        error: (error) => {
          this.error.set(getErrorMessage(error));
          this.loading.set(false);
        },
        complete: () => this.loading.set(false),
      });
  }

  openCreate(): void {
    this.form.reset({ name: '', description: '' });
    this.showCreate.set(true);
    afterNextRender(() => this.projectNameInput().focus());
  }

  closeCreate(): void {
    if (!this.saving()) {
      this.showCreate.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  closeCreateOnEscape(): void {
    if (this.showCreate()) {
      this.closeCreate();
    }
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    this.api
      .createProject({ name: value.name.trim(), description: value.description.trim() || null })
      .subscribe({
        next: () => {
          this.showCreate.set(false);
          this.load();
        },
        error: (error) => {
          this.error.set(getErrorMessage(error));
          this.saving.set(false);
        },
        complete: () => this.saving.set(false),
      });
  }

  toggleFollowingOnly(): void {
    this.followingOnly.update((value) => !value);
    this.load();
  }

  toggleFollow(project: ProjectSummary): void {
    if (project.currentUserRole === 'Owner') {
      return;
    }

    this.followBusyId.set(project.id);
    this.error.set(null);
    const operation = project.isFollowing
      ? this.api.unfollowProject(project.id)
      : this.api.followProject(project.id);

    operation.subscribe({
      next: () => this.load(),
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.followBusyId.set(null);
      },
      complete: () => this.followBusyId.set(null),
    });
  }

  isDefaultProject(project: ProjectSummary): boolean {
    return project.id === this.defaultProjectId();
  }

  setDefaultProject(project: ProjectSummary): void {
    if (!project.isFollowing) {
      return;
    }

    this.defaultProjectId.set(project.id);
    this.saveProjectDocumentsPreference({ defaultProjectId: project.id });
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
    localStorage.setItem(
      this.projectDocumentsPreferenceKey(),
      JSON.stringify({ ...this.readProjectDocumentsPreference(), ...change }),
    );
  }

  private projectDocumentsPreferenceKey(): string {
    return `${PROJECT_DOCUMENTS_PREFERENCE_PREFIX}${this.auth.currentUser()?.id ?? 'anonymous'}`;
  }
}
