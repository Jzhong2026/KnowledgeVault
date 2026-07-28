import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { getErrorMessage } from '../../../core/http/error-message';
import {
  Category,
  Comment,
  DocumentScope,
  KnowledgeItem,
  Revision,
  RevisionSummary,
  SaveDocumentRequest,
  Tag,
} from '../../../core/models/knowledge.models';
import { ProjectSummary, ProjectTopic } from '../../../core/models/projects.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';
import { StatusPill } from '../../../shared/components/status-pill/status-pill';
import { MermaidDiagramsDirective } from '../../../shared/directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../../shared/pipes/markdown-content.pipe';
import { KnowledgeEditor } from '../components/knowledge-editor/knowledge-editor';

@Component({
  selector: 'app-knowledge-detail-page',
  imports: [
    DatePipe,
    FormsModule,
    KnowledgeEditor,
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
  private static readonly collapsedCommentThreshold = 1200;

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
  readonly expandedCommentIds = signal<ReadonlySet<string>>(new Set());
  readonly copiedTarget = signal<string | null>(null);
  readonly copyError = signal<string | null>(null);

  // ----- Inline editor state -----
  readonly editorOpen = signal(false);
  readonly editorSaving = signal(false);
  readonly editorItem = signal<KnowledgeItem | null>(null);
  readonly editorCategories = signal<Category[]>([]);
  readonly editorTags = signal<Tag[]>([]);
  readonly editorProjects = signal<ProjectSummary[]>([]);
  readonly editorTopics = signal<ProjectTopic[]>([]);
  private currentItemId: string | null = null;

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Knowledge item id is missing.');
      this.loading.set(false);
      return;
    }

    this.loadItem(id);
  }

  /**
   * Re-fetch the knowledge item and refresh the detail surface. Called on
   * initial load and after the inline editor persists a change so the page
   * always shows the latest revision content.
   */
  private loadItem(id: string): void {
    this.currentItemId = id;
    this.loading.set(true);
    this.error.set(null);
    this.viewingRevision.set(null);

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

  async copyDocumentContent(): Promise<void> {
    const item = this.item();
    if (!item) {
      return;
    }

    const revision = this.viewingRevision();
    const content = revision?.content ?? item.content;
    const revisionNumber = revision?.revisionNumber ?? item.currentRevisionNumber;
    await this.copyValue(this.formatContentForCopy(content), `revision:${revisionNumber}`);
  }

  downloadDocument(): void {
    const current = this.item();
    if (!current) {
      return;
    }
    this.api.downloadDocument(current.id).subscribe({
      next: (blob) => this.triggerBrowserDownload(blob, this.documentFileName(current.title)),
      error: (err: unknown) => this.error.set(getErrorMessage(err)),
    });
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

  // ----- Inline editor -----

  openEditor(): void {
    const item = this.item();
    if (!item || item.documentType === 'ProjectMemory') {
      return;
    }

    this.editorItem.set(item);
    this.editorOpen.set(true);
    this.editorSaving.set(false);

    // Preload the editor's reference data (categories, tags, projects, topics)
    // so the dialog opens with the same options the workspace page uses.
    forkJoin({
      categories: this.api.listCategories(),
      tags: this.api.listTags(),
      projects: this.api.listProjects({ followingOnly: true, pageSize: 100 }),
    }).subscribe({
      next: ({ categories, tags, projects }) => {
        this.editorCategories.set(categories);
        this.editorTags.set(tags);
        this.editorProjects.set(projects.items);
        if (item.projectId) {
          this.loadEditorTopics(item.projectId);
        }
      },
      error: () => {
        this.editorCategories.set([]);
        this.editorTags.set([]);
        this.editorProjects.set([]);
      },
    });
  }

  closeEditor(): void {
    this.editorOpen.set(false);
    this.editorItem.set(null);
  }

  saveEdit(request: SaveDocumentRequest): void {
    const id = this.currentItemId;
    if (!id) {
      return;
    }

    this.editorSaving.set(true);
    // Editing must preserve the document's current folder location, so drop
    // any folderId the editor defaulted to (root) before sending the request.
    const payload: SaveDocumentRequest = { ...request, folderId: undefined };
    this.api.updateKnowledgeItem(id, payload).subscribe({
      next: () => {
        this.editorOpen.set(false);
        this.editorItem.set(null);
        this.editorSaving.set(false);
        // Refresh the detail surface so the user sees the latest content
        // and revision number after a successful edit.
        this.loadItem(id);
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.editorSaving.set(false);
      },
    });
  }

  deleteFromEditor(): void {
    const id = this.currentItemId;
    if (!id) {
      return;
    }

    this.editorSaving.set(true);
    this.api.deleteKnowledgeItem(id).subscribe({
      next: () => {
        this.editorOpen.set(false);
        this.editorItem.set(null);
        this.editorSaving.set(false);
        // Navigate back to the list so the detail page does not render a
        // missing document.
        void this.router.navigate([this.documentListRoute]);
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.editorSaving.set(false);
      },
    });
  }

  onEditorProjectSelected(projectId: string): void {
    this.loadEditorTopics(projectId);
  }

  private loadEditorTopics(projectId: string): void {
    this.api.listTopics(projectId).subscribe({
      next: (result) => this.editorTopics.set(result.items),
      error: () => this.editorTopics.set([]),
    });
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

  isCommentCollapsed(comment: Comment): boolean {
    return (
      comment.content.length > KnowledgeDetailPage.collapsedCommentThreshold &&
      !this.expandedCommentIds().has(comment.id)
    );
  }

  toggleCommentExpansion(commentId: string): void {
    this.expandedCommentIds.update((expanded) => {
      const next = new Set(expanded);
      if (next.has(commentId)) {
        next.delete(commentId);
      } else {
        next.add(commentId);
      }
      return next;
    });
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

  private sanitizeFileName(name: string): string {
    const sanitized = name.replace(/[\\/:*?"<>|]/g, '_').trim();
    return sanitized || 'document';
  }

  private documentFileName(title: string): string {
    const fileName = this.sanitizeFileName(title);
    return /\.[^./\\\s]+$/.test(fileName) ? fileName : `${fileName}.md`;
  }

  private formatContentForCopy(content: string): string {
    try {
      return JSON.stringify(JSON.parse(content), null, 2);
    } catch {
      return content;
    }
  }

  private triggerBrowserDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
