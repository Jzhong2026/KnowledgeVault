import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { ProjectSummary } from '../../../core/models/projects.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

@Component({
  selector: 'app-projects-page',
  imports: [ReactiveFormsModule, RouterLink, LoadingIndicator],
  templateUrl: './projects-page.html',
  styleUrl: './projects-page.css',
})
export class ProjectsPage {
  private readonly api = inject(ApiClient);
  private readonly fb = new FormBuilder();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly projects = signal<ProjectSummary[]>([]);
  readonly showCreate = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    description: ['' as string | null],
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listProjects({ pageSize: 50 }).subscribe({
      next: (result) => this.projects.set(result.items),
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  toggleCreate(): void {
    this.showCreate.update((value) => !value);
    this.form.reset({ name: '', description: null });
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    this.api
      .createProject({ name: value.name, description: value.description || null })
      .subscribe({
        next: () => {
          this.toggleCreate();
          this.load();
        },
        error: (error) => this.error.set(getErrorMessage(error)),
        complete: () => this.saving.set(false),
      });
  }
}
