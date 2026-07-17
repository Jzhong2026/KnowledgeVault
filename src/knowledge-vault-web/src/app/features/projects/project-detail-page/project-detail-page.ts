import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { Project, ProjectGroup } from '../../../core/models/projects.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

@Component({
  selector: 'app-project-detail-page',
  imports: [ReactiveFormsModule, RouterLink, LoadingIndicator],
  templateUrl: './project-detail-page.html',
  styleUrl: './project-detail-page.css',
})
export class ProjectDetailPage {
  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly projectId = this.route.snapshot.paramMap.get('id');

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly followBusy = signal(false);
  readonly error = signal<string | null>(null);
  readonly project = signal<Project | null>(null);
  readonly groups = signal<ProjectGroup[]>([]);
  readonly showCreateGroup = signal(false);
  readonly canManageGroups = computed(() => {
    const role = this.project()?.currentUserRole;
    return role === 'Owner' || role === 'Editor';
  });

  readonly groupForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    description: ['', [Validators.maxLength(512)]],
  });

  constructor() {
    if (!this.projectId) {
      this.error.set('Project id is missing.');
      this.loading.set(false);
      return;
    }

    this.loadProject();
  }

  loadProject(): void {
    if (!this.projectId) {
      return;
    }

    this.loading.set(true);
    this.api.getProject(this.projectId).subscribe({
      next: (project) => {
        this.project.set(project);
        if (project.isFollowing) {
          this.loadGroups();
        } else {
          this.groups.set([]);
          this.loading.set(false);
        }
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  loadGroups(): void {
    if (!this.projectId) {
      return;
    }

    this.api.listGroups(this.projectId).subscribe({
      next: (result) => this.groups.set(result.items),
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
      complete: () => this.loading.set(false),
    });
  }

  toggleFollow(): void {
    const project = this.project();
    if (!project || project.currentUserRole === 'Owner') {
      return;
    }

    this.followBusy.set(true);
    const operation = project.isFollowing
      ? this.api.unfollowProject(project.id)
      : this.api.followProject(project.id);
    operation.subscribe({
      next: () => this.loadProject(),
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.followBusy.set(false);
      },
      complete: () => this.followBusy.set(false),
    });
  }

  toggleCreateGroup(): void {
    this.showCreateGroup.update((value) => !value);
    this.groupForm.reset({ name: '', description: '' });
  }

  createGroup(): void {
    const project = this.project();
    if (!project || this.groupForm.invalid) {
      this.groupForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.groupForm.getRawValue();
    this.api.createGroup(project.id, {
      name: value.name.trim(),
      description: value.description.trim() || null,
    }).subscribe({
      next: () => {
        this.showCreateGroup.set(false);
        this.loadGroups();
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.saving.set(false);
      },
      complete: () => this.saving.set(false),
    });
  }

  deleteGroup(groupId: string): void {
    const project = this.project();
    if (!project || project.currentUserRole !== 'Owner') {
      return;
    }

    this.api.deleteGroup(project.id, groupId).subscribe({
      next: () => this.loadGroups(),
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }
}
