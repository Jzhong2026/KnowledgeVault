import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import {
  Comment,
  DocumentScope,
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
  private readonly router = inject(Router);

  readonly item = signal<KnowledgeItem | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly workspaceScope =
    (this.route.snapshot.data?.['scope'] as DocumentScope | undefined) ?? 'Personal';
  readonly documentListRoute =
    this.workspaceScope === 'Project' ? '/project-documents' : '/knowledge';
  readonly documentListLabel =
    this.workspaceScope === 'Project' ? 'Back to project documents' : 'Back to my documents';

  readonly revisions = signal<RevisionSummary[]>([]);
  readonly comments = signal<Comment[]>([]);
  readonly viewingRevision = signal<Revision | null>(null);
  readonly addingComment = signal(false);
  readonly newComment = signal('');
  readonly copiedTarget = signal<string | null>(null);
  readonly copyError = signal<string | null>(null);

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Knowledge item id is missing.');
      this.loading.set(false);
      return;
    }

    this.api.getKnowledgeItem(id).subscribe({
      next: (item) => {
        if (item.scope !== this.workspaceScope) {
          const detailRoute =
            item.scope === 'Project' ? '/project-documents/detail' : '/knowledge/detail';
          void this.router.navigate([detailRoute, item.id], { replaceUrl: true });
          return;
        }

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

  async copyDocumentMarkdown(): Promise<void> {
    const item = this.item();
    if (!item) {
      return;
    }

    const revision = this.viewingRevision();
    const content = revision?.content ?? item.content;
    const revisionNumber = revision?.revisionNumber ?? item.currentRevisionNumber;
    await this.copyValue(content, `revision:${revisionNumber}`);
  }

  async copyComment(comment: Comment): Promise<void> {
    if (comment.isDeleted) {
      return;
    }

    await this.copyValue(comment.content, `comment:${comment.id}`);
  }

  documentCopyTarget(): string | null {
    const item = this.item();
    if (!item) {
      return null;
    }

    return `revision:${this.viewingRevision()?.revisionNumber ?? item.currentRevisionNumber}`;
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

  private async copyValue(value: string, target: string): Promise<void> {
    this.copyError.set(null);
    if (await this.copyText(value)) {
      this.copiedTarget.set(target);
      return;
    }

    this.copyError.set('Unable to access the clipboard. Please copy the text manually.');
  }

  private async copyText(value: string): Promise<boolean> {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      try {
        await navigator.clipboard.writeText(value);
        return true;
      } catch {
        // Fall through to the legacy browser clipboard path.
      }
    }

    if (typeof document === 'undefined') {
      return false;
    }

    const textArea = document.createElement('textarea');
    textArea.value = value;
    textArea.setAttribute('readonly', '');
    textArea.style.position = 'fixed';
    textArea.style.opacity = '0';
    document.body.appendChild(textArea);
    textArea.select();

    try {
      return document.execCommand('copy');
    } finally {
      textArea.remove();
    }
  }
}
