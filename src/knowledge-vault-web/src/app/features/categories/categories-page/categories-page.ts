import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { Category } from '../../../core/models/knowledge.models';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

@Component({
  selector: 'app-categories-page',
  imports: [ReactiveFormsModule, EmptyState, LoadingIndicator],
  templateUrl: './categories-page.html',
  styleUrl: './categories-page.css',
})
export class CategoriesPage {
  private readonly api = inject(ApiClient);
  private readonly fb = inject(FormBuilder);

  readonly categories = signal<Category[]>([]);
  readonly selected = signal<Category | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    description: [''],
    color: ['#2f80ed'],
    sortOrder: [0],
    isArchived: [false],
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listCategories(true).subscribe({
      next: (categories) => this.categories.set(categories),
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  edit(category: Category): void {
    this.selected.set(category);
    this.form.reset({
      name: category.name,
      description: category.description ?? '',
      color: category.color ?? '#2f80ed',
      sortOrder: category.sortOrder,
      isArchived: category.isArchived,
    });
  }

  newCategory(): void {
    this.selected.set(null);
    this.form.reset({
      name: '',
      description: '',
      color: '#2f80ed',
      sortOrder: 0,
      isArchived: false,
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const request = {
      name: value.name,
      description: value.description || null,
      color: value.color || null,
      sortOrder: value.sortOrder,
      isArchived: value.isArchived,
    };
    const selected = this.selected();
    const operation = selected
      ? this.api.updateCategory(selected.id, request)
      : this.api.createCategory(request);

    operation.subscribe({
      next: (category) => {
        this.selected.set(category);
        this.load();
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.saving.set(false);
      },
      complete: () => this.saving.set(false),
    });
  }

  delete(category: Category): void {
    this.api.deleteCategory(category.id).subscribe({
      next: () => {
        if (this.selected()?.id === category.id) {
          this.newCategory();
        }
        this.load();
      },
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }
}
