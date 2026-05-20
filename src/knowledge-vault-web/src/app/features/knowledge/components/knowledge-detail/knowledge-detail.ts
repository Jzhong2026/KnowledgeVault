import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

import { KnowledgeItem } from '../../../../core/models/knowledge.models';
import { StatusPill } from '../../../../shared/components/status-pill/status-pill';
import { MarkdownContentPipe } from '../../../../shared/pipes/markdown-content.pipe';

@Component({
  selector: 'app-knowledge-detail',
  imports: [DatePipe, MarkdownContentPipe, StatusPill],
  templateUrl: './knowledge-detail.html',
  styleUrl: './knowledge-detail.css',
})
export class KnowledgeDetail {
  @Input({ required: true }) item!: KnowledgeItem;

  @Output() closeDetail = new EventEmitter<void>();
  @Output() editItem = new EventEmitter<void>();

  hasContent(): boolean {
    return this.item.content.trim().length > 0;
  }
}
