import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { KnowledgeItemSummary } from '../../../../core/models/knowledge.models';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { StatusPill } from '../../../../shared/components/status-pill/status-pill';

@Component({
  selector: 'app-knowledge-list',
  imports: [DatePipe, EmptyState, RouterLink, StatusPill],
  templateUrl: './knowledge-list.html',
  styleUrl: './knowledge-list.css',
})
export class KnowledgeList {
  @Input({ required: true }) items: KnowledgeItemSummary[] = [];
  @Input() selectedId: string | null = null;

  @Output() editKnowledgeItem = new EventEmitter<string>();

  openEditor(id: string, event: MouseEvent): void {
    event.stopPropagation();
    this.editKnowledgeItem.emit(id);
  }
}
