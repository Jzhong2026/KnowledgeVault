import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import {
  Category,
  KnowledgeItem,
  KnowledgeItemStatus,
  KnowledgeItemSummary,
  SaveKnowledgeItemRequest,
  Tag,
} from '../../../core/models/knowledge.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';
import { KnowledgeEditor } from '../components/knowledge-editor/knowledge-editor';
import { KnowledgeList } from '../components/knowledge-list/knowledge-list';

@Component({
  selector: 'app-knowledge-page',
  imports: [FormsModule, KnowledgeEditor, KnowledgeList, LoadingIndicator],
  templateUrl: './knowledge-page.html',
  styleUrl: './knowledge-page.css',
})
export class KnowledgePage {
  private readonly api = inject(ApiClient);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly items = signal<KnowledgeItemSummary[]>([]);
  readonly selectedItem = signal<KnowledgeItem | null>(null);
  readonly selectedId = signal<string | null>(null);
  readonly editorOpen = signal(false);
  readonly categories = signal<Category[]>([]);
  readonly tags = signal<Tag[]>([]);
  readonly search = signal('');
  readonly status = signal<KnowledgeItemStatus | ''>('');

  constructor() {
    this.loadWorkspace();
  }

  loadWorkspace(): void {
    this.loading.set(true);
    forkJoin({
      categories: this.api.listCategories(),
      tags: this.api.listTags(),
      knowledge: this.api.listKnowledgeItems({ page: 1, pageSize: 50 }),
    }).subscribe({
      next: ({ categories, tags, knowledge }) => {
        this.categories.set(categories);
        this.tags.set(tags);
        this.items.set(knowledge.items);
      },
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  applyFilters(): void {
    this.loading.set(true);
    this.api.listKnowledgeItems({
      page: 1,
      pageSize: 50,
      search: this.search(),
      status: this.status() || undefined,
    }).subscribe({
      next: (result) => this.items.set(result.items),
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  selectItem(id: string): void {
    this.selectedId.set(id);
    this.api.getKnowledgeItem(id).subscribe({
      next: (item) => {
        this.selectedItem.set(item);
        this.editorOpen.set(true);
      },
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }

  createNew(): void {
    this.selectedId.set(null);
    this.selectedItem.set(null);
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }

  save(request: SaveKnowledgeItemRequest): void {
    this.saving.set(true);
    const item = this.selectedItem();
    const operation = item
      ? this.api.updateKnowledgeItem(item.id, request)
      : this.api.createKnowledgeItem(request);

    operation.subscribe({
      next: (saved) => {
        this.selectedItem.set(saved);
        this.selectedId.set(saved.id);
        this.editorOpen.set(false);
        this.loadWorkspace();
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.saving.set(false);
      },
      complete: () => this.saving.set(false),
    });
  }

  deleteSelected(): void {
    const item = this.selectedItem();
    if (!item) {
      return;
    }

    this.saving.set(true);
    this.api.deleteKnowledgeItem(item.id).subscribe({
      next: () => {
        this.selectedId.set(null);
        this.selectedItem.set(null);
        this.editorOpen.set(false);
        this.loadWorkspace();
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.saving.set(false);
      },
      complete: () => this.saving.set(false),
    });
  }
}
