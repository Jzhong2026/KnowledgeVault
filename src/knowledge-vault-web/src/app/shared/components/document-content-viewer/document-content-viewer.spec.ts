import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DocumentContentViewer } from './document-content-viewer';

describe('DocumentContentViewer', () => {
  let fixture: ComponentFixture<DocumentContentViewer>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DocumentContentViewer] }).compileComponents();
    fixture = TestBed.createComponent(DocumentContentViewer);
  });

  it('renders Markdown through the existing Markdown pipeline', () => {
    fixture.componentRef.setInput('title', 'notes.md');
    fixture.componentRef.setInput('content', '# Heading');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1')?.textContent).toBe('Heading');
  });

  it('renders txt literally and preserves whitespace', () => {
    fixture.componentRef.setInput('title', 'notes.txt');
    fixture.componentRef.setInput('content', '# Not a heading\n  indented');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1')).toBeNull();
    expect(fixture.nativeElement.querySelector('pre')?.textContent).toBe('# Not a heading\n  indented');
  });

  it('formats valid JSON without changing the input', () => {
    const content = '{"name":"vault","enabled":true}';
    fixture.componentRef.setInput('title', 'settings.json');
    fixture.componentRef.setInput('content', content);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('pre')?.textContent).toContain('\n  "name": "vault"');
    expect(fixture.componentInstance.content).toBe(content);
  });

  it('shows invalid JSON as its original text', () => {
    fixture.componentRef.setInput('title', 'broken.json');
    fixture.componentRef.setInput('content', '{broken');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('pre')?.textContent).toBe('{broken');
  });

  it('renders HTML in a script-free sandbox without external resource access', () => {
    fixture.componentRef.setInput('title', 'workflow.html');
    fixture.componentRef.setInput('content', `<!doctype html>
      <html>
        <head>
          <meta http-equiv="refresh" content="0;url=https://external.example/refresh">
          <style>h1 { color: rgb(1, 2, 3); }</style>
        </head>
        <body onload="window.parent.compromised = true">
          <h1 id="section">Workflow</h1>
          <button data-language="en">English</button>
          <a href="#section">Jump locally</a>
          <a href="https://external.example/page">Leave preview</a>
          <img src="https://external.example/pixel.png" onerror="alert(1)">
          <iframe src="https://external.example/frame"></iframe>
          <script>document.body.dataset.scriptExecuted = 'true';</script>
        </body>
      </html>`);
    fixture.detectChanges();

    const frame = fixture.nativeElement.querySelector('iframe') as HTMLIFrameElement;
    expect(frame).toBeTruthy();
    expect(frame.getAttribute('sandbox')).toBe('');
    expect(frame.getAttribute('referrerpolicy')).toBe('no-referrer');

    const preview = frame.srcdoc;
    expect(preview).toContain('<style>h1 { color: rgb(1, 2, 3); }</style>');
    expect(preview).toContain('href="#section"');
    expect(preview).toContain("default-src 'none'");
    expect(preview).toContain("script-src 'none'");
    expect(preview).not.toMatch(/<script\b/i);
    expect(preview).not.toMatch(/\sonload=/i);
    expect(preview).not.toMatch(/<iframe\b/i);
    expect(preview).not.toMatch(/<button\b/i);
    expect(preview).not.toContain('external.example');
  });
});
