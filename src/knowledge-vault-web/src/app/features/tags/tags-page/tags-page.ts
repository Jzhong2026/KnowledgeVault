import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { Tag } from '../../../core/models/knowledge.models';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

@Component({
  selector: 'app-tags-page',
  imports: [ReactiveFormsModule, EmptyState, LoadingIndicator],
  templateUrl: './tags-page.html',
  styleUrl: './tags-page.css',
})
export class TagsPage {
  private readonly api = inject(ApiClient);
  private readonly fb = inject(FormBuilder);

  readonly tags = signal<Tag[]>([]);
  readonly selected = signal<Tag | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly editorOpen = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(64)]],
    color: ['#27ae60'],
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listTags().subscribe({
      next: (tags) => this.tags.set(tags),
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  edit(tag: Tag): void {
    this.selected.set(tag);
    this.form.reset({
      name: tag.name,
      color: tag.color ?? '#27ae60',
    });
    this.editorOpen.set(true);
  }

  newTag(): void {
    this.selected.set(null);
    this.form.reset({
      name: '',
      color: '#27ae60',
    });
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const selected = this.selected();
    const operation = selected
      ? this.api.updateTag(selected.id, value)
      : this.api.createTag(value);

    operation.subscribe({
      next: (tag) => {
        this.selected.set(tag);
        this.editorOpen.set(false);
        this.load();
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.saving.set(false);
      },
      complete: () => this.saving.set(false),
    });
  }

  delete(tag: Tag): void {
    this.api.deleteTag(tag.id).subscribe({
      next: () => {
        if (this.selected()?.id === tag.id) {
          this.selected.set(null);
          this.editorOpen.set(false);
        }
        this.load();
      },
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }
}
