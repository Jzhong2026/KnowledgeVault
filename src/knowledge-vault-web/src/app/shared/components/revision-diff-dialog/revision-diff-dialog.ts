import { Component, EventEmitter, Input, OnChanges, Output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';

import { Revision } from '../../../core/models/knowledge.models';
import { RevisionDiffBlock, buildRevisionDiff } from '../../utils/revision-diff';

@Component({
  selector: 'app-revision-diff-dialog',
  imports: [DatePipe],
  templateUrl: './revision-diff-dialog.html',
  styleUrl: './revision-diff-dialog.css',
})
export class RevisionDiffDialog implements OnChanges {
  @Input({ required: true }) previousRevision!: Revision;
  @Input({ required: true }) selectedRevision!: Revision;
  @Output() closeDialog = new EventEmitter<void>();

  readonly leftWidth = signal(50);
  readonly expandedBlockIndexes = signal<ReadonlySet<number>>(new Set());
  readonly blocks = signal<RevisionDiffBlock[]>([]);
  private syncingScroll = false;

  ngOnChanges(): void {
    if (this.previousRevision && this.selectedRevision) {
      this.blocks.set(buildRevisionDiff(this.previousRevision.content, this.selectedRevision.content));
      this.expandedBlockIndexes.set(new Set());
    }
  }

  onDividerPointerDown(event: PointerEvent): void {
    const container = (event.currentTarget as HTMLElement).parentElement;
    if (!container) {
      return;
    }
    const startX = event.clientX;
    const startWidth = this.leftWidth();
    const width = container.getBoundingClientRect().width;
    const onMove = (moveEvent: PointerEvent) => {
      this.leftWidth.set(Math.min(75, Math.max(25, startWidth + ((moveEvent.clientX - startX) / width) * 100)));
    };
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  }

  toggleBlock(index: number): void {
    this.expandedBlockIndexes.update((expanded) => {
      const next = new Set(expanded);
      if (next.has(index)) {
        next.delete(index);
      } else {
        next.add(index);
      }
      return next;
    });
  }

  syncScroll(event: Event, otherPaneClass: string): void {
    if (this.syncingScroll) {
      return;
    }
    const source = event.target as HTMLElement;
    const other = source.closest('.revision-diff')?.querySelector<HTMLElement>(`.${otherPaneClass}`);
    if (!other) {
      return;
    }
    this.syncingScroll = true;
    other.scrollTop = source.scrollTop;
    other.scrollLeft = source.scrollLeft;
    requestAnimationFrame(() => this.syncingScroll = false);
  }
}
