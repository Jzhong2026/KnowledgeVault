import { Component, computed, input } from '@angular/core';

import { KnowledgeItemStatus } from '../../../core/models/knowledge.models';

@Component({
  selector: 'app-status-pill',
  templateUrl: './status-pill.html',
  styleUrl: './status-pill.css',
})
export class StatusPill {
  readonly status = input.required<KnowledgeItemStatus>();

  readonly className = computed(() => `status-pill status-pill--${this.status().toLowerCase()}`);
}
