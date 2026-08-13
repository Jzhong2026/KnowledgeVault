import { Component, Input } from '@angular/core';

import { MermaidDiagramsDirective } from '../../directives/mermaid-diagrams.directive';
import { MarkdownContentPipe } from '../../pipes/markdown-content.pipe';
import {
  DocumentContentKind,
  formatJsonContent,
  getDocumentContentKind,
} from '../../utils/document-content-kind';

@Component({
  selector: 'app-document-content-viewer',
  imports: [MarkdownContentPipe, MermaidDiagramsDirective],
  template: `
    @if (contentKind() === 'markdown') {
      <div
        class="markdown-body"
        [class.markdown-body--wide]="wide"
        [innerHTML]="content | markdownContent"
        [appMermaidDiagrams]="content"
      ></div>
    } @else {
      <pre class="plain-content" [class.plain-content--json]="contentKind() === 'json'"><code>{{ displayContent() }}</code></pre>
    }
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .plain-content {
      margin: 0;
      overflow: auto;
      border: 1px solid var(--border, #e2e8f0);
      border-radius: 10px;
      background: #f8fafc;
      color: var(--text, #0f172a);
      font: 13px/1.65 ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', monospace;
      tab-size: 4;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      padding: 16px;
    }
    .plain-content--json { white-space: pre; }
    .plain-content code { font: inherit; }
  `,
})
export class DocumentContentViewer {
  @Input() title: string | null | undefined = '';
  @Input() content = '';
  @Input() wide = false;

  contentKind(): DocumentContentKind {
    return getDocumentContentKind(this.title);
  }

  displayContent(): string {
    return this.contentKind() === 'json' ? formatJsonContent(this.content) : this.content;
  }
}
