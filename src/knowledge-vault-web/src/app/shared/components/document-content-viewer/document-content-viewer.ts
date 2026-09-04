import { isPlatformBrowser } from '@angular/common';
import { Component, Input, OnChanges, PLATFORM_ID, inject, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

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
    } @else if (contentKind() === 'html') {
      @if (htmlPreview(); as preview) {
        <iframe
          class="static-html-preview"
          sandbox
          referrerpolicy="no-referrer"
          loading="lazy"
          [attr.title]="title ? title + ' static HTML preview' : 'Static HTML preview'"
          [srcdoc]="preview"
        ></iframe>
      } @else {
        <p class="html-preview-fallback">Static HTML preview is available in the browser.</p>
      }
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
    .static-html-preview {
      display: block;
      width: 100%;
      height: min(72vh, 900px);
      min-height: 520px;
      border: 1px solid var(--border, #e2e8f0);
      border-radius: 10px;
      background: #fff;
    }
    .html-preview-fallback { color: var(--muted, #64748b); }
  `,
})
export class DocumentContentViewer implements OnChanges {
  @Input() title: string | null | undefined = '';
  @Input() content = '';
  @Input() wide = false;

  readonly htmlPreview = signal<SafeHtml | null>(null);

  private readonly sanitizer = inject(DomSanitizer);
  private readonly platformId = inject(PLATFORM_ID);

  ngOnChanges(): void {
    if (this.contentKind() !== 'html' || !isPlatformBrowser(this.platformId)) {
      this.htmlPreview.set(null);
      return;
    }

    const staticDocument = buildStaticHtmlDocument(this.content);
    this.htmlPreview.set(this.sanitizer.bypassSecurityTrustHtml(staticDocument));
  }

  contentKind(): DocumentContentKind {
    return getDocumentContentKind(this.title);
  }

  displayContent(): string {
    return this.contentKind() === 'json' ? formatJsonContent(this.content) : this.content;
  }
}

const blockedElements = [
  'applet',
  'base',
  'button',
  'embed',
  'frame',
  'frameset',
  'iframe',
  'link',
  'object',
  'script',
];

const urlAttributes = new Set([
  'action',
  'archive',
  'background',
  'cite',
  'codebase',
  'data',
  'formaction',
  'href',
  'longdesc',
  'manifest',
  'ping',
  'poster',
  'src',
  'srcset',
  'xlink:href',
]);

const staticHtmlContentSecurityPolicy = [
  "default-src 'none'",
  "style-src 'unsafe-inline'",
  'img-src data:',
  'font-src data:',
  'media-src data:',
  "script-src 'none'",
  "connect-src 'none'",
  "object-src 'none'",
  "frame-src 'none'",
  "child-src 'none'",
  "worker-src 'none'",
  "form-action 'none'",
  "base-uri 'none'",
].join('; ');

function buildStaticHtmlDocument(content: string): string {
  const previewDocument = new DOMParser().parseFromString(content, 'text/html');

  previewDocument.querySelectorAll(blockedElements.join(',')).forEach((element) => element.remove());
  previewDocument.querySelectorAll('meta[http-equiv]').forEach((element) => element.remove());

  previewDocument.querySelectorAll('*').forEach((element) => {
    for (const attribute of Array.from(element.attributes)) {
      const name = attribute.name.toLowerCase();
      const value = attribute.value.trim();

      if (name.startsWith('on') || name === 'srcdoc' || name === 'target' || name === 'download') {
        element.removeAttribute(attribute.name);
        continue;
      }

      if (!urlAttributes.has(name)) {
        continue;
      }

      const isLocalFragment = (name === 'href' || name === 'xlink:href') && value.startsWith('#');
      const isEmbeddedRasterImage = name === 'src'
        && /^data:image\/(?:avif|gif|jpeg|png|webp);base64,/i.test(value);

      if (!isLocalFragment && !isEmbeddedRasterImage) {
        element.removeAttribute(attribute.name);
      }
    }
  });

  const contentSecurityPolicy = previewDocument.createElement('meta');
  contentSecurityPolicy.setAttribute('http-equiv', 'Content-Security-Policy');
  contentSecurityPolicy.setAttribute('content', staticHtmlContentSecurityPolicy);
  previewDocument.head.prepend(contentSecurityPolicy);

  return `<!doctype html>\n${previewDocument.documentElement.outerHTML}`;
}
