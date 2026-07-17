import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { Project, ProjectTopic } from '../../../core/models/projects.models';
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
  private readonly fb = new FormBuilder();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly project = signal<Project | null>(null);
  readonly topics = signal<ProjectTopic[]>([]);
  readonly showCreateTopic = signal(false);

  readonly topicForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    description: ['' as string | null],
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Project id is missing.');
      this.loading.set(false);
      return;
    }

    this.api.getProject(id).subscribe({
      next: (project) => {
        this.project.set(project);
        this.loadTopics(id);
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  loadTopics(projectId: string): void {
    this.api.listTopics(projectId).subscribe({
      next: (result) => {
        this.topics.set(result.items);
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  toggleCreateTopic(): void {
    this.showCreateTopic.update((value) => !value);
    this.topicForm.reset({ name: '', description: null });
  }

  createTopic(): void {
    const project = this.project();
    if (!project || this.topicForm.invalid) {
      this.topicForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.topicForm.getRawValue();
    this.api
      .createTopic(project.id, { name: value.name, description: value.description || null })
      .subscribe({
        next: () => {
          this.toggleCreateTopic();
          this.loadTopics(project.id);
        },
        error: (error) => this.error.set(getErrorMessage(error)),
        complete: () => this.saving.set(false),
      });
  }

  deleteTopic(topicId: string): void {
    const project = this.project();
    if (!project) {
      return;
    }

    this.api.deleteTopic(project.id, topicId).subscribe({
      next: () => this.loadTopics(project.id),
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }
}
