import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { MermaidDiagramsDirective } from '../../../../shared/directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../../../shared/pipes/markdown-content.pipe';

@Component({
  selector: 'app-content-editor',
  imports: [FormsModule, MarkdownContentPipe, MermaidDiagramsDirective],
  templateUrl: './content-editor.html',
  styleUrl: './content-editor.css',
})
export class ContentEditor implements OnChanges {
  @Input() content = '';
  @Input() saving = false;

  @Output() cancelEdit = new EventEmitter<void>();
  @Output() saveContent = new EventEmitter<string>();

  draft = '';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['content']) {
      this.draft = this.content;
    }
  }

  save(): void {
    this.saveContent.emit(this.draft);
  }
}
