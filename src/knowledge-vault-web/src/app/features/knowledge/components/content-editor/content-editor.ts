import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { MermaidDiagramsDirective } from '../../../../shared/directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../../../shared/pipes/markdown-content.pipe';
import { ConfirmService } from '../../../../shared/components/confirm-dialog/confirm-dialog';

@Component({
  selector: 'app-content-editor',
  imports: [FormsModule, MarkdownContentPipe, MermaidDiagramsDirective],
  templateUrl: './content-editor.html',
  styleUrl: './content-editor.css',
})
export class ContentEditor implements OnChanges, AfterViewInit, OnDestroy {
  @Input() content = '';
  @Input() saving = false;

  @Output() cancelEdit = new EventEmitter<void>();
  @Output() saveContent = new EventEmitter<string>();

  @ViewChild('previewEl') previewEl?: ElementRef<HTMLElement>;
  @ViewChild('sourceEl') sourceEl?: ElementRef<HTMLTextAreaElement>;

  draft = '';
  readonly previewWidth = signal(50);

  private readonly confirm = inject(ConfirmService);
  private readonly cdr = inject(ChangeDetectorRef);

  /** Guards against feedback loops between the two panes' scroll handlers. */
  private syncingScroll = false;
  /** Coalesces repeated edit events so preview scrolls once per render tick. */
  private previewSyncQueued = false;
  private pendingPreviewRatio: number | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['content']) {
      this.draft = this.content;
    }
  }

  ngAfterViewInit(): void {
    // Reset preview scroll whenever a fresh document is loaded.
    this.queuePreviewScroll(0);
  }

  ngOnDestroy(): void {
    this.previewSyncQueued = false;
    this.pendingPreviewRatio = null;
  }

  save(): void {
    this.saveContent.emit(this.draft);
  }

  async requestClose(): Promise<void> {
    if (this.saving) {
      return;
    }

    if (this.draft !== this.content) {
      const ok = await this.confirm.confirm({
        title: 'Discard changes?',
        message: 'You have unsaved content edits. Closing will lose them.',
        confirmLabel: 'Discard',
        cancelLabel: 'Keep editing',
        intent: 'danger',
      });
      if (!ok) {
        return;
      }
    }

    this.cancelEdit.emit();
  }

  onDraftInput(nextDraft: string): void {
    this.draft = nextDraft;
    this.queuePreviewSyncFromCaret();
  }

  onSourceScroll(): void {
    if (this.syncingScroll || !this.previewEl || !this.sourceEl) {
      return;
    }
    const textarea = this.sourceEl.nativeElement;
    const maxScroll = textarea.scrollHeight - textarea.clientHeight;
    if (maxScroll <= 0) {
      return;
    }
    const ratio = textarea.scrollTop / maxScroll;
    const previewMax = this.previewEl.nativeElement.scrollHeight - this.previewEl.nativeElement.clientHeight;
    if (previewMax <= 0) {
      return;
    }
    this.syncingScroll = true;
    this.previewEl.nativeElement.scrollTop = ratio * previewMax;
    // Release the guard on the next frame so the resulting `scroll` event
    // from the preview doesn't recursively re-trigger us.
    requestAnimationFrame(() => (this.syncingScroll = false));
  }

  syncPreviewToCaret(): void {
    this.queuePreviewSyncFromCaret();
  }

  onDividerPointerDown(event: PointerEvent): void {
    const container = (event.currentTarget as HTMLElement).parentElement;
    if (!container) {
      return;
    }

    const startX = event.clientX;
    const startWidth = this.previewWidth();
    const width = container.getBoundingClientRect().width;
    const onMove = (moveEvent: PointerEvent) => {
      this.previewWidth.set(
        Math.min(75, Math.max(25, startWidth + ((moveEvent.clientX - startX) / width) * 100)),
      );
      // Keep scroll positions in sync after the pane resizes.
      this.cdr.detectChanges();
    };
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      this.onSourceScroll();
    };

    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    void this.requestClose();
  }

  private queuePreviewSyncFromCaret(): void {
    const textarea = this.sourceEl?.nativeElement;
    if (!textarea) {
      return;
    }

    const contentLength = Math.max(textarea.value.length, 1);
    const caretOffset = Math.max(0, Math.min(textarea.selectionStart ?? contentLength, contentLength));
    this.queuePreviewScroll(caretOffset / contentLength);
  }

  private queuePreviewScroll(ratio: number): void {
    this.pendingPreviewRatio = Math.min(1, Math.max(0, ratio));
    if (this.previewSyncQueued) {
      return;
    }

    this.previewSyncQueued = true;
    queueMicrotask(() => {
      this.previewSyncQueued = false;
      const nextRatio = this.pendingPreviewRatio;
      this.pendingPreviewRatio = null;
      if (nextRatio === null) {
        return;
      }

      this.scrollPreviewToRatio(nextRatio);
    });
  }

  private scrollPreviewToRatio(ratio: number): void {
    const preview = this.previewEl?.nativeElement;
    if (!preview) {
      return;
    }

    const max = preview.scrollHeight - preview.clientHeight;
    if (max <= 0) {
      return;
    }

    this.syncingScroll = true;
    preview.scrollTop = ratio * max;
    requestAnimationFrame(() => (this.syncingScroll = false));
  }
}