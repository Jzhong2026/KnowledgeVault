import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  SimpleChanges,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ConfirmService } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { DocumentContentViewer } from '../../../../shared/components/document-content-viewer/document-content-viewer';
import { MermaidDiagramsDirective } from '../../../../shared/directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../../../shared/pipes/markdown-content.pipe';
import {
  DocumentContentKind,
  formatJsonContent,
  getDocumentPreviewLabel,
  getDocumentSourceLabel,
} from '../../../../shared/utils/document-content-kind';

@Component({
  selector: 'app-fullscreen-document-workspace',
  imports: [FormsModule, MarkdownContentPipe, MermaidDiagramsDirective, DocumentContentViewer],
  templateUrl: './fullscreen-document-workspace.html',
  styleUrl: './fullscreen-document-workspace.css',
})
export class FullscreenDocumentWorkspace implements OnChanges, OnInit, OnDestroy {
  @Input() title = '';
  @Input() content = '';
  @Input() revisionLabel = 'Latest';
  @Input() contentKind: DocumentContentKind = 'markdown';
  @Input() canEdit = false;
  @Input() startEditing = false;
  @Input() saving = false;

  @Output() closeWorkspace = new EventEmitter<void>();
  @Output() saveContent = new EventEmitter<string>();

  @ViewChild('previewEl') previewEl?: ElementRef<HTMLElement>;
  @ViewChild('sourceViewEl') sourceViewEl?: ElementRef<HTMLElement>;
  @ViewChild('sourceEditorEl') sourceEditorEl?: ElementRef<HTMLTextAreaElement>;

  readonly theme = signal<'light' | 'dark'>('light');
  readonly editing = signal(false);
  readonly sourceWidth = signal(50);
  draft = '';
  lines: string[] = [];
  private readonly confirm = inject(ConfirmService);
  private syncingScroll = false;
  private previousBodyOverflow = '';
  private previousDocumentOverflow = '';
  private previousBodyUserSelect = '';

  ngOnInit(): void {
    if (typeof document === 'undefined') return;
    this.previousBodyOverflow = document.body.style.overflow;
    this.previousDocumentOverflow = document.documentElement.style.overflow;
    document.body.style.overflow = 'hidden';
    document.documentElement.style.overflow = 'hidden';
  }

  ngOnDestroy(): void {
    if (typeof document === 'undefined') return;
    document.body.style.overflow = this.previousBodyOverflow;
    document.documentElement.style.overflow = this.previousDocumentOverflow;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['content']) {
      const draftBeforeRefresh = this.draft;
      this.draft = this.content;
      this.lines = this.content.split('\n');
      // A successful save returns the submitted content through the parent.
      // Stay in the full-screen workspace, but return it to read-only mode.
      if (this.editing() && this.content === draftBeforeRefresh) {
        this.editing.set(false);
      }
    }
    if (changes['startEditing'] && this.startEditing && this.canEdit) this.beginEdit();
  }

  setTheme(theme: 'light' | 'dark'): void { this.theme.set(theme); }

  beginEdit(): void {
    if (!this.canEdit || this.saving) return;
    this.draft = this.content;
    this.editing.set(true);
  }

  save(): void {
    if (this.saving) return;
    this.saveContent.emit(this.draft);
  }

  async cancelEdit(): Promise<void> {
    if (this.saving) return;
    if (this.hasUnsavedChanges()) {
      const discard = await this.confirm.confirm({
        title: 'Discard changes?',
        message: 'You have unsaved content edits. Cancelling will lose them.',
        confirmLabel: 'Discard',
        cancelLabel: 'Keep editing',
        intent: 'danger',
      });
      if (!discard) return;
    }
    this.draft = this.content;
    this.editing.set(false);
  }

  async requestClose(): Promise<void> {
    if (this.saving) return;
    if (this.editing() && this.hasUnsavedChanges()) {
      const discard = await this.confirm.confirm({
        title: 'Discard changes?',
        message: 'You have unsaved content edits. Closing will lose them.',
        confirmLabel: 'Discard',
        cancelLabel: 'Keep editing',
        intent: 'danger',
      });
      if (!discard) return;
    }
    this.closeWorkspace.emit();
  }

  private hasUnsavedChanges(): boolean {
    return this.draft !== this.content;
  }

  onSourceScroll(): void {
    if (this.syncingScroll || !this.previewEl) return;
    const source = this.sourceEditorEl?.nativeElement ?? this.sourceViewEl?.nativeElement;
    if (!source) return;
    const sourceMax = source.scrollHeight - source.clientHeight;
    const preview = this.previewEl.nativeElement;
    const previewMax = preview.scrollHeight - preview.clientHeight;
    if (sourceMax <= 0 || previewMax <= 0) return;
    this.syncingScroll = true;
    preview.scrollTop = (source.scrollTop / sourceMax) * previewMax;
    requestAnimationFrame(() => (this.syncingScroll = false));
  }

  onPreviewScroll(): void {
    if (this.syncingScroll || !this.previewEl) return;
    const source = this.sourceEditorEl?.nativeElement ?? this.sourceViewEl?.nativeElement;
    const preview = this.previewEl.nativeElement;
    if (!source) return;
    const previewMax = preview.scrollHeight - preview.clientHeight;
    const sourceMax = source.scrollHeight - source.clientHeight;
    if (previewMax <= 0 || sourceMax <= 0) return;
    this.syncingScroll = true;
    source.scrollTop = (preview.scrollTop / previewMax) * sourceMax;
    requestAnimationFrame(() => (this.syncingScroll = false));
  }

  onDividerPointerDown(event: PointerEvent): void {
    event.preventDefault();
    const container = (event.currentTarget as HTMLElement).parentElement;
    if (!container) return;
    (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
    this.previousBodyUserSelect = document.body.style.userSelect;
    document.body.style.userSelect = 'none';
    const width = container.getBoundingClientRect().width;
    const startX = event.clientX;
    const startWidth = this.sourceWidth();
    const move = (next: PointerEvent) => this.sourceWidth.set(Math.min(75, Math.max(25, startWidth + ((next.clientX - startX) / width) * 100)));
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); document.body.style.userSelect = this.previousBodyUserSelect; };
    window.addEventListener('pointermove', move); window.addEventListener('pointerup', up);
  }

  previewContent(): string {
    const value = this.editing() ? this.draft : this.content;
    return this.contentKind === 'json' ? formatJsonContent(value) : value;
  }
  sourceLabel(): string {
    return getDocumentSourceLabel(this.contentKind);
  }
  previewLabel(): string {
    return getDocumentPreviewLabel(this.contentKind, 'fullscreen');
  }
}
