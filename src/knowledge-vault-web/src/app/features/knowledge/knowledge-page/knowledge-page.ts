import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import {
  Category,
  DocumentScope,
  KnowledgeItem,
  KnowledgeItemStatus,
  KnowledgeItemSummary,
  SaveDocumentRequest,
  Tag,
} from '../../../core/models/knowledge.models';
import { ProjectSummary, ProjectTopic } from '../../../core/models/projects.models';
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
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly workspaceScope =
    (this.route.snapshot.data['scope'] as DocumentScope | undefined) ?? 'Personal';
  readonly isProjectWorkspace = this.workspaceScope === 'Project';

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly items = signal<KnowledgeItemSummary[]>([]);
  readonly selectedItem = signal<KnowledgeItem | null>(null);
  readonly selectedId = signal<string | null>(null);
  readonly editorOpen = signal(false);
  readonly categories = signal<Category[]>([]);
  readonly tags = signal<Tag[]>([]);
  readonly projects = signal<ProjectSummary[]>([]);
  readonly filterGroups = signal<ProjectTopic[]>([]);
  readonly editorGroups = signal<ProjectTopic[]>([]);
  readonly search = signal('');
  readonly status = signal<KnowledgeItemStatus | ''>('');
  readonly projectId = signal<string | null>(null);
  readonly topicId = signal<string | null>(null);

  constructor() {
    this.projectId.set(this.route.snapshot.queryParamMap.get('projectId'));
    this.topicId.set(this.route.snapshot.queryParamMap.get('topicId'));
    this.loadWorkspace();
  }

  loadWorkspace(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      categories: this.api.listCategories(),
      tags: this.api.listTags(),
      knowledge: this.api.listKnowledgeItems({
        scope: this.workspaceScope,
        page: 1,
        pageSize: 50,
        projectId: this.projectId() ?? undefined,
        topicId: this.topicId() ?? undefined,
      }),
      projects: this.api.listProjects({ followingOnly: true, pageSize: 100 }),
    }).subscribe({
      next: ({ categories, tags, knowledge, projects }) => {
        this.categories.set(categories);
        this.tags.set(tags);
        this.items.set(knowledge.items);
        this.projects.set(projects.items);
        if (this.isProjectWorkspace && this.projectId()) {
          this.loadFilterGroups(this.projectId()!);
        }
      },
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  applyFilters(): void {
    this.loading.set(true);
    this.api
      .listKnowledgeItems({
        scope: this.workspaceScope,
        page: 1,
        pageSize: 50,
        search: this.search(),
        status: this.status() || undefined,
        projectId: this.projectId() ?? undefined,
        topicId: this.topicId() ?? undefined,
      })
      .subscribe({
        next: (result) => this.items.set(result.items),
        error: (error) => this.error.set(getErrorMessage(error)),
        complete: () => this.loading.set(false),
      });
  }

  onProjectFilterChange(projectId: string): void {
    this.projectId.set(projectId || null);
    this.topicId.set(null);
    this.filterGroups.set([]);

    if (projectId) {
      this.loadFilterGroups(projectId);
    }

    this.updateQueryParams();
    this.applyFilters();
  }

  onGroupFilterChange(topicId: string): void {
    this.topicId.set(topicId || null);
    this.updateQueryParams();
    this.applyFilters();
  }

  private loadFilterGroups(projectId: string): void {
    this.api.listGroups(projectId).subscribe({
      next: (result) => this.filterGroups.set(result.items),
      error: (error) => {
        this.filterGroups.set([]);
        this.error.set(getErrorMessage(error));
      },
    });
  }

  private updateQueryParams(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        projectId: this.projectId(),
        topicId: this.topicId(),
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  onProjectSelected(projectId: string): void {
    this.api.listTopics(projectId).subscribe({
      next: (result) => this.editorGroups.set(result.items),
      error: () => this.editorGroups.set([]),
    });
  }

  editItem(id: string): void {
    this.selectedId.set(id);
    this.api.getKnowledgeItem(id).subscribe({
      next: (item) => {
        this.selectedItem.set(item);
        if (item.scope === 'Project' && item.projectId) {
          this.api.listTopics(item.projectId).subscribe({
            next: (result) => this.editorGroups.set(result.items),
            error: () => this.editorGroups.set([]),
          });
        }
        this.editorOpen.set(true);
      },
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }

  createNew(): void {
    if (this.isProjectWorkspace && this.projects().length === 0) {
      this.error.set('Follow a project before creating or querying project documents.');
      return;
    }

    if (this.isProjectWorkspace && !this.projectId() && this.projects().length === 1) {
      const projectId = this.projects()[0].id;
      this.projectId.set(projectId);
      this.loadFilterGroups(projectId);
      this.updateQueryParams();
    }

    if (this.isProjectWorkspace && this.projectId()) {
      this.onProjectSelected(this.projectId()!);
    } else {
      this.editorGroups.set([]);
    }

    this.selectedId.set(null);
    this.selectedItem.set(null);
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }

  save(request: SaveDocumentRequest): void {
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
