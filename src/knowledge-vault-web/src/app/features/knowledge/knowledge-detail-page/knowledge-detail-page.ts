import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import {
  Comment,
  KnowledgeItem,
  Revision,
  RevisionSummary,
} from '../../../core/models/knowledge.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';
import { StatusPill } from '../../../shared/components/status-pill/status-pill';
import { MermaidDiagramsDirective } from '../../../shared/directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../../shared/pipes/markdown-content.pipe';

@Component({
  selector: 'app-knowledge-detail-page',
  imports: [
    DatePipe,
    FormsModule,
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

  readonly revisions = signal<RevisionSummary[]>([]);
  readonly comments = signal<Comment[]>([]);
  readonly viewingRevision = signal<Revision | null>(null);
  readonly addingComment = signal(false);
  readonly newComment = signal('');

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Knowledge item id is missing.');
      this.loading.set(false);
      return;
    }

    this.api.getKnowledgeItem(id).subscribe({
      next: (item) => {
        this.item.set(item);
        this.loadRevisions(id);
        this.loadComments(id, item.currentRevisionNumber);
      },
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loading.set(false),
    });
  }

  private loadRevisions(documentId: string): void {
    this.api.listRevisions(documentId).subscribe({
      next: (result) => this.revisions.set(result.items),
      error: () => this.revisions.set([]),
    });
  }

  private loadComments(documentId: string, revisionNumber: number): void {
    this.api.listComments(documentId, revisionNumber).subscribe({
      next: (result) => this.comments.set(result.items),
      error: () => this.comments.set([]),
    });
  }

  viewRevision(revisionNumber: number): void {
    const item = this.item();
    if (!item) {
      return;
    }

    this.api.getRevision(item.id, revisionNumber).subscribe({
      next: (revision) => this.viewingRevision.set(revision),
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }

  backToCurrent(): void {
    this.viewingRevision.set(null);
  }

  addComment(): void {
    const content = this.newComment().trim();
    const item = this.item();
    if (!item || !content) {
      return;
    }

    this.addingComment.set(true);
    this.api.addComment(item.id, item.currentRevisionNumber, content).subscribe({
      next: (comment) => {
        this.comments.set([...this.comments(), comment]);
        this.newComment.set('');
      },
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.addingComment.set(false),
    });
  }

  hasContent(content: string): boolean {
    return content.trim().length > 0;
  }
}
