import { Component, computed, inject, signal } from '@angular/core';
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
import { FolderSummary } from '../../../core/models/folder.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';
import { StatusPill } from '../../../shared/components/status-pill/status-pill';
import { RevisionDiffDialog } from '../../../shared/components/revision-diff-dialog/revision-diff-dialog';
import { DocumentContentViewer } from '../../../shared/components/document-content-viewer/document-content-viewer';
import { MermaidDiagramsDirective } from '../../../shared/directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../../shared/pipes/markdown-content.pipe';
import { KnowledgeEditor } from '../components/knowledge-editor/knowledge-editor';
import { ContentEditor } from '../components/content-editor/content-editor';
import { getDocumentContentKind } from '../../../shared/utils/document-content-kind';
import { FullscreenDocumentWorkspace } from '../components/fullscreen-document-workspace/fullscreen-document-workspace';

@Component({
  selector: 'app-knowledge-detail-page',
  imports: [
    DatePipe,
    FormsModule,
    KnowledgeEditor,
    ContentEditor,
    DocumentContentViewer,
    FullscreenDocumentWorkspace,
    LoadingIndicator,
    MarkdownContentPipe,
    MermaidDiagramsDirective,
    RevisionDiffDialog,
    RouterLink,
    StatusPill,
  ],
  templateUrl: './knowledge-detail-page.html',
  styleUrl: './knowledge-detail-page.css',
})
export class KnowledgeDetailPage {
  private static readonly collapsedCommentThreshold = 1200;
  private static readonly commentPageSize = 20;

  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly getDocumentContentKind = getDocumentContentKind;

  readonly item = signal<KnowledgeItem | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly workspaceScope =
    (this.route.snapshot.data?.['scope'] as DocumentScope | undefined) ?? 'Personal';
  readonly documentListRoute =
    this.workspaceScope === 'Project' ? '/project-documents' : '/knowledge';
  readonly documentListLabel =
    this.workspaceScope === 'Project' ? 'Back to project documents' : 'Back to my documents';
  readonly documentProjectName = signal<string | null>(null);
  readonly documentBreadcrumb = signal<Array<{ id: string; name: string }>>([]);

  readonly revisions = signal<RevisionSummary[]>([]);
  readonly comments = signal<Comment[]>([]);
  readonly commentPage = signal(0);
  readonly commentTotalCount = signal(0);
  readonly loadingMoreComments = signal(false);
  readonly viewingRevision = signal<Revision | null>(null);
  readonly addingComment = signal(false);
  readonly newComment = signal('');
  readonly expandedCommentIds = signal<ReadonlySet<string>>(new Set());
  readonly copiedTarget = signal<string | null>(null);
  readonly copyError = signal<string | null>(null);
  readonly revisionComparison = signal<{ previous: Revision; selected: Revision } | null>(null);
  readonly revisionComparisonLoading = signal(false);
  readonly revisionsCollapsed = signal(false);
  readonly historicalRevisions = computed(() => {
    const currentRevisionNumber = this.item()?.currentRevisionNumber;
    return this.revisions().filter(
      (revision) => currentRevisionNumber === undefined || revision.revisionNumber < currentRevisionNumber,
    );
  });

  // ----- Inline editor state -----
  readonly editorOpen = signal(false);
  readonly editorSaving = signal(false);
  readonly editorItem = signal<KnowledgeItem | null>(null);
  readonly editorCategories = signal<Category[]>([]);
  readonly editorTags = signal<Tag[]>([]);
  readonly editorProjects = signal<ProjectSummary[]>([]);
  readonly editorTopics = signal<ProjectTopic[]>([]);
  readonly contentEditorOpen = signal(false);
  readonly contentEditorSaving = signal(false);
  readonly fullscreenDocumentOpen = signal(false);
  readonly fullscreenDocumentStartsEditing = signal(false);
  readonly displayedContent = computed(() => this.viewingRevision()?.content ?? this.item()?.content ?? '');
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
        this.loadDocumentProject(item);
        this.loadDocumentBreadcrumb(item);
        this.loadRevisions(id);
        this.loadComments(id);
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

  private loadComments(documentId: string, page = 1): void {
    this.api.listDocumentComments(documentId, page, KnowledgeDetailPage.commentPageSize).subscribe({
      next: (result) => {
        this.comments.update((comments) => (page === 1 ? result.items : [...comments, ...result.items]));
        this.commentPage.set(result.page);
        this.commentTotalCount.set(result.totalCount);
      },
      error: () => {
        if (page === 1) {
          this.comments.set([]);
          this.commentPage.set(0);
          this.commentTotalCount.set(0);
        }
      },
    });
  }

  hasMoreComments(): boolean {
    return this.comments().length < this.commentTotalCount();
  }

  loadMoreComments(): void {
    const documentId = this.currentItemId;
    if (!documentId || this.loadingMoreComments() || !this.hasMoreComments()) {
      return;
    }

    this.loadingMoreComments.set(true);
    const nextPage = this.commentPage() + 1;
    this.api.listDocumentComments(documentId, nextPage, KnowledgeDetailPage.commentPageSize).subscribe({
      next: (result) => {
        this.comments.update((comments) => [...comments, ...result.items]);
        this.commentPage.set(result.page);
        this.commentTotalCount.set(result.totalCount);
      },
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.loadingMoreComments.set(false),
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

  toggleRevisions(): void {
    this.revisionsCollapsed.update((collapsed) => !collapsed);
  }

  openContentEditor(): void { this.openFullscreenDocument(true); }

  closeContentEditor(): void {
    this.contentEditorOpen.set(false);
  }

  private loadDocumentProject(item: KnowledgeItem): void {
    this.documentProjectName.set(item.projectName ?? null);
    if (item.scope !== 'Project' || !item.projectId || item.projectName) {
      return;
    }

    this.api.getProject(item.projectId).subscribe({
      next: (project) => this.documentProjectName.set(project.name),
      error: () => this.documentProjectName.set(null),
    });
  }

  private loadDocumentBreadcrumb(item: KnowledgeItem): void {
    this.documentBreadcrumb.set([]);
    if (item.scope !== 'Project') {
      return;
    }

    const queryProjectId = this.route.snapshot.queryParamMap?.get('projectId');
    const folderId = this.route.snapshot.queryParamMap?.get('browseFolderId');
    if (!folderId || (queryProjectId && queryProjectId !== item.projectId)) {
      return;
    }

    const chain: FolderSummary[] = [];
    let nextId: string | null = folderId;
    const maxDepth = 32;

    const finish = (): void => {
      chain.reverse();
      this.documentBreadcrumb.set(chain.map((folder) => ({ id: folder.id, name: folder.name })));
    };

    const visit = (depth: number): void => {
      if (!nextId || depth > maxDepth) {
        finish();
        return;
      }

      this.api.getFolder(nextId).subscribe({
        next: (folder) => {
          if (folder.scope !== 'Project' || folder.projectId !== item.projectId) {
            finish();
            return;
          }
          chain.push(folder);
          nextId = folder.parentFolderId ?? null;
          visit(depth + 1);
        },
        error: () => finish(),
      });
    };

    visit(0);
  }

  openFullscreenDocument(startEditing = false): void {
    if (this.item()) {
      this.fullscreenDocumentOpen.set(true);
      this.fullscreenDocumentStartsEditing.set(startEditing);
    }
  }

  isFullscreenSupported(): boolean { return !!this.item(); }

  displayedContentTitle(): string {
    return this.viewingRevision()?.title ?? this.item()?.title ?? '';
  }

  closeFullscreenDocument(): void {
    this.fullscreenDocumentOpen.set(false);
    this.fullscreenDocumentStartsEditing.set(false);
  }

  saveContent(content: string): void {
    const item = this.item();
    if (!item || this.contentEditorSaving()) {
      return;
    }

    this.contentEditorSaving.set(true);
    const payload: SaveDocumentRequest = {
      scope: item.scope,
      projectId: item.projectId ?? null,
      topicId: item.topicId ?? null,
      documentType: item.documentType,
      title: item.title,
      content,
      summary: item.summary ?? null,
      sourceUrl: item.sourceUrl ?? null,
      linkDisplayText: item.linkDisplayText ?? null,
      linkUrl: item.linkUrl ?? null,
      changeNote: null,
      categoryId: item.category?.id ?? null,
      status: item.status,
      tagIds: item.tags.map((tag) => tag.id),
      tagNames: [],
      expectedRevisionNumber: item.currentRevisionNumber,
    };

    this.api.updateKnowledgeItem(item.id, payload).subscribe({
      next: (updated) => {
        this.contentEditorOpen.set(false);
        // Keep the full-screen reader mounted after a save. The update
        // response is already the latest item, so a navigation-style reload
        // (which previously dismissed the reader) is unnecessary.
        this.item.set(updated);
        this.viewingRevision.set(null);
        this.loadRevisions(item.id);
        this.loadComments(item.id);
      },
      error: (error) => this.error.set(getErrorMessage(error)),
      complete: () => this.contentEditorSaving.set(false),
    });
  }

  compareRevisionWithPrevious(revisionNumber: number): void {
    const item = this.item();
    if (!item || revisionNumber <= 1 || this.revisionComparisonLoading()) {
      return;
    }

    this.revisionComparisonLoading.set(true);
    this.api.getRevision(item.id, revisionNumber - 1).subscribe({
      next: (previous) => {
        this.api.getRevision(item.id, revisionNumber).subscribe({
          next: (selected) => this.revisionComparison.set({ previous, selected }),
          error: (error) => this.error.set(getErrorMessage(error)),
          complete: () => this.revisionComparisonLoading.set(false),
        });
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.revisionComparisonLoading.set(false);
      },
    });
  }

  closeRevisionComparison(): void {
    this.revisionComparison.set(null);
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
        this.comments.update((comments) => [comment, ...comments]);
        this.commentTotalCount.update((totalCount) => totalCount + 1);
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
