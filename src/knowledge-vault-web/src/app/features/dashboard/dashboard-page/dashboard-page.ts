import { Component, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { Category, KnowledgeItemSummary, Tag } from '../../../core/models/knowledge.models';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';
import { StatusPill } from '../../../shared/components/status-pill/status-pill';

@Component({
  selector: 'app-dashboard-page',
  imports: [EmptyState, LoadingIndicator, StatusPill],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
})
export class DashboardPage {
  private readonly api = inject(ApiClient);

  readonly loading = signal(true);
  readonly categories = signal<Category[]>([]);
  readonly tags = signal<Tag[]>([]);
  readonly recentItems = signal<KnowledgeItemSummary[]>([]);
  readonly totalItems = signal(0);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    forkJoin({
      categories: this.api.listCategories(),
      tags: this.api.listTags(),
      knowledge: this.api.listKnowledgeItems({ page: 1, pageSize: 5 }),
    }).subscribe({
      next: ({ categories, tags, knowledge }) => {
        this.categories.set(categories);
        this.tags.set(tags);
        this.recentItems.set(knowledge.items);
        this.totalItems.set(knowledge.totalCount);
      },
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }
}
