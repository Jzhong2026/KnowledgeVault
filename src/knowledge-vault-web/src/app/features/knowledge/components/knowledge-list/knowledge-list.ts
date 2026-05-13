import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DatePipe } from '@angular/common';

import { KnowledgeItemSummary } from '../../../../core/models/knowledge.models';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { StatusPill } from '../../../../shared/components/status-pill/status-pill';

@Component({
  selector: 'app-knowledge-list',
  imports: [DatePipe, EmptyState, StatusPill],
  templateUrl: './knowledge-list.html',
  styleUrl: './knowledge-list.css',
})
export class KnowledgeList {
  @Input({ required: true }) items: KnowledgeItemSummary[] = [];
  @Input() selectedId: string | null = null;

  @Output() selectItem = new EventEmitter<string>();
}
