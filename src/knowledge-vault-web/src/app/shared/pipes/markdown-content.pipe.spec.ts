import { MarkdownContentPipe } from './markdown-content.pipe';

describe('MarkdownContentPipe', () => {
  const pipe = new MarkdownContentPipe();

  it('creates a Mermaid placeholder for mermaid code fences', () => {
    const result = pipe.transform('```mermaid\nflowchart LR\n  A --> B\n```');

    expect(result).toContain('class="mermaid-diagram"');
    expect(result).toContain('class="mermaid-source"');
    expect(result).toContain('flowchart LR');
    expect(result).not.toContain('class="hljs');
  });

  it('escapes Mermaid source before adding it to the page', () => {
    const result = pipe.transform('```mermaid\nA[<script>alert(1)</script>]\n```');

    expect(result).toContain('&lt;script&gt;');
    expect(result).not.toContain('<script>');
  });
});
