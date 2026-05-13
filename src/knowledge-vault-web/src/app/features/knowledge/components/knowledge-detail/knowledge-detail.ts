import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

import { KnowledgeItem } from '../../../../core/models/knowledge.models';
import { StatusPill } from '../../../../shared/components/status-pill/status-pill';

@Component({
  selector: 'app-knowledge-detail',
  imports: [DatePipe, StatusPill],
  templateUrl: './knowledge-detail.html',
  styleUrl: './knowledge-detail.css',
})
export class KnowledgeDetail {
  @Input({ required: true }) item!: KnowledgeItem;

  @Output() closeDetail = new EventEmitter<void>();
  @Output() editItem = new EventEmitter<void>();

  paragraphs(): string[] {
    return this.item.content
      .split(/\n{2,}/)
      .map((part) => part.trim())
      .filter(Boolean);
  }
}
