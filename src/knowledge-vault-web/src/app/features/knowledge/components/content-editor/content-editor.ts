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
  /** Last value before the current keystroke. Used to detect where the edit
   *  occurred so we can scroll the preview to the matching location. */
  private previousDraft = '';
  readonly previewWidth = signal(50);

  private readonly confirm = inject(ConfirmService);
  private readonly cdr = inject(ChangeDetectorRef);

  /** Guards against feedback loops between the two panes' scroll handlers. */
  private syncingScroll = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['content']) {
      this.draft = this.content;
      this.previousDraft = this.content;
    }
  }

  ngAfterViewInit(): void {
    // Reset preview scroll whenever a fresh document is loaded.
    queueMicrotask(() => this.scrollPreviewTo(0));
  }

  ngOnDestroy(): void {
    // No subscriptions to dispose, but kept for symmetry / future cleanup.
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

  onDraftInput(): void {
    const next = this.draft;
    const prev = this.previousDraft;
    this.previousDraft = next;

    if (next === prev) {
      return;
    }
    const offset = this.firstChangeOffset(prev, next);
    if (offset < 0) {
      return;
    }
    this.scrollPreviewToOffset(offset, next.length || 1);
  }

  onPreviewScroll(): void {
    // Preview doesn't directly drive the source — content has different
    // lengths per line, so we only mirror source→preview (not preview→source)
    // to avoid the two fighting each other.
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

  /**
   * Return the index of the first character where `prev` and `next` differ,
   * or `-1` when one is a prefix of the other (pure insertion / deletion at
   * the tail). The caller treats `-1` as "nothing to scroll to".
   */
  private firstChangeOffset(prev: string, next: string): number {
    const min = Math.min(prev.length, next.length);
    for (let i = 0; i < min; i++) {
      if (prev.charCodeAt(i) !== next.charCodeAt(i)) {
        return i;
      }
    }
    return -1;
  }

  /** Scroll the preview so the character at `offset` is roughly in view. */
  private scrollPreviewToOffset(offset: number, total: number): void {
    const preview = this.previewEl?.nativeElement;
    if (!preview) {
      return;
    }
    const ratio = total === 0 ? 0 : Math.min(1, Math.max(0, offset / total));
    const max = preview.scrollHeight - preview.clientHeight;
    if (max <= 0) {
      return;
    }
    this.syncingScroll = true;
    preview.scrollTop = ratio * max;
    requestAnimationFrame(() => (this.syncingScroll = false));
  }

  private scrollPreviewTo(top: number): void {
    const preview = this.previewEl?.nativeElement;
    if (!preview) {
      return;
    }
    preview.scrollTop = top;
  }
}