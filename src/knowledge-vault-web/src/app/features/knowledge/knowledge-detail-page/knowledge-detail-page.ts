import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { KnowledgeItem } from '../../../core/models/knowledge.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';
import { StatusPill } from '../../../shared/components/status-pill/status-pill';
import { MermaidDiagramsDirective } from '../../../shared/directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../../shared/pipes/markdown-content.pipe';

@Component({
  selector: 'app-knowledge-detail-page',
  imports: [
    DatePipe,
    LoadingIndicator,
    MarkdownContentPipe,
    MermaidDiagramsDirective,
    RouterLink,
    StatusPill,
  ],
  templateUrl: './knowledge-detail-page.html',
  styleUrl: './knowledge-detail-page.css',
})
export class KnowledgeDetailPage {
  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);

  readonly item = signal<KnowledgeItem | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Knowledge item id is missing.');
      this.loading.set(false);
      return;
    }

    this.api.getKnowledgeItem(id).subscribe({
      next: (item) => this.item.set(item),
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  hasContent(content: string): boolean {
    return content.trim().length > 0;
  }
}
