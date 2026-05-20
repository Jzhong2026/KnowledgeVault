import { Pipe, PipeTransform } from '@angular/core';
import hljs from 'highlight.js/lib/core';
import bash from 'highlight.js/lib/languages/bash';
import csharp from 'highlight.js/lib/languages/csharp';
import css from 'highlight.js/lib/languages/css';
import javascript from 'highlight.js/lib/languages/javascript';
import json from 'highlight.js/lib/languages/json';
import markdownLanguage from 'highlight.js/lib/languages/markdown';
import plaintext from 'highlight.js/lib/languages/plaintext';
import powershell from 'highlight.js/lib/languages/powershell';
import python from 'highlight.js/lib/languages/python';
import typescript from 'highlight.js/lib/languages/typescript';
import xml from 'highlight.js/lib/languages/xml';
import yaml from 'highlight.js/lib/languages/yaml';
import { Marked, Renderer, type Tokens } from 'marked';

hljs.registerLanguage('bash', bash);
hljs.registerLanguage('csharp', csharp);
hljs.registerLanguage('css', css);
hljs.registerLanguage('javascript', javascript);
hljs.registerLanguage('json', json);
hljs.registerLanguage('markdown', markdownLanguage);
hljs.registerLanguage('plaintext', plaintext);
hljs.registerLanguage('powershell', powershell);
hljs.registerLanguage('python', python);
hljs.registerLanguage('typescript', typescript);
hljs.registerLanguage('xml', xml);
hljs.registerLanguage('yaml', yaml);

const renderer = new Renderer();

renderer.code = ({ text, lang }: Tokens.Code): string => {
  const language = normalizeLanguage(lang);
  const highlighted = language
    ? hljs.highlight(text, { language, ignoreIllegals: true }).value
    : escapeHtml(text);
  const languageClass = language ? ` language-${escapeAttribute(language)}` : '';

  return `<pre><code class="hljs${languageClass}">${highlighted}</code></pre>`;
};

renderer.codespan = ({ text }: Tokens.Codespan): string => `<code>${escapeHtml(text)}</code>`;
renderer.html = ({ text }: Tokens.HTML | Tokens.Tag): string => escapeHtml(text);
renderer.link = function ({ href, title, tokens }: Tokens.Link): string {
  const safeHref = isSafeUrl(href) ? escapeAttribute(href) : '#';
  const titleAttribute = title ? ` title="${escapeAttribute(title)}"` : '';
  const label = this.parser.parseInline(tokens);

  return `<a href="${safeHref}"${titleAttribute} target="_blank" rel="noreferrer noopener">${label}</a>`;
};

const markdown = new Marked({
  async: false,
  breaks: false,
  gfm: true,
  renderer,
});

@Pipe({
  name: 'markdownContent',
})
export class MarkdownContentPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    const content = value?.trim();

    if (!content) {
      return '';
    }

    try {
      return markdown.parse(content, { async: false });
    } catch {
      return `<pre><code>${escapeHtml(content)}</code></pre>`;
    }
  }
}

function normalizeLanguage(language: string | undefined): string {
  const normalized = language?.trim().split(/\s+/)[0].toLowerCase() ?? '';

  if (!normalized) {
    return '';
  }

  const aliases: Record<string, string> = {
    cs: 'csharp',
    html: 'xml',
    js: 'javascript',
    md: 'markdown',
    py: 'python',
    ps1: 'powershell',
    shell: 'bash',
    sh: 'bash',
    ts: 'typescript',
    yml: 'yaml',
  };

  const languageName = aliases[normalized] ?? normalized;

  return hljs.getLanguage(languageName) ? languageName : '';
}

function isSafeUrl(url: string): boolean {
  const trimmed = url.trim();

  return /^(https?:|mailto:|\/|#)/i.test(trimmed) || !/^[a-z][a-z0-9+.-]*:/i.test(trimmed);
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function escapeAttribute(value: string): string {
  return escapeHtml(value).replace(/`/g, '&#96;');
}
