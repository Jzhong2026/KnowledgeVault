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
});
