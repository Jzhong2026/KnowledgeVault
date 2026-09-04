import { TestBed } from '@angular/core/testing';

import { FullscreenDocumentWorkspace } from './fullscreen-document-workspace';

describe('FullscreenDocumentWorkspace content formats', () => {
  it('shows plain text literally', async () => {
    await TestBed.configureTestingModule({ imports: [FullscreenDocumentWorkspace] }).compileComponents();
    const fixture = TestBed.createComponent(FullscreenDocumentWorkspace);
    fixture.componentRef.setInput('title', 'notes.txt');
    fixture.componentRef.setInput('content', '# Literal\n  spacing');
    fixture.componentRef.setInput('contentKind', 'text');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.fullscreen-document__preview h1')).toBeNull();
    expect(fixture.nativeElement.querySelector('.fullscreen-document__preview')?.textContent)
      .toBe('# Literal\n  spacing');
  });

  it('formats JSON preview while saving the raw source', async () => {
    await TestBed.configureTestingModule({ imports: [FullscreenDocumentWorkspace] }).compileComponents();
    const fixture = TestBed.createComponent(FullscreenDocumentWorkspace);
    const content = '{"name":"vault"}';
    fixture.componentRef.setInput('title', 'settings.json');
    fixture.componentRef.setInput('content', content);
    fixture.componentRef.setInput('contentKind', 'json');
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    const saved = vi.fn();
    fixture.componentInstance.saveContent.subscribe(saved);

    expect(fixture.nativeElement.querySelector('.fullscreen-document__preview')?.textContent)
      .toContain('\n  "name": "vault"');
    fixture.componentInstance.beginEdit();
    fixture.componentInstance.save();
    expect(saved).toHaveBeenCalledWith(content);
  });

  it('renders HTML as a static sandboxed document', async () => {
    await TestBed.configureTestingModule({ imports: [FullscreenDocumentWorkspace] }).compileComponents();
    const fixture = TestBed.createComponent(FullscreenDocumentWorkspace);
    fixture.componentRef.setInput('title', 'workflow.html');
    fixture.componentRef.setInput('content', '<!doctype html><html><body><h1>Workflow</h1></body></html>');
    fixture.componentRef.setInput('contentKind', 'html');
    fixture.detectChanges();

    const frame = fixture.nativeElement.querySelector('iframe') as HTMLIFrameElement;
    expect(frame).toBeTruthy();
    expect(frame.getAttribute('sandbox')).toBe('');
    expect(frame.srcdoc).toContain('<h1>Workflow</h1>');
    expect(fixture.nativeElement.querySelector('.fullscreen-document__modebar')?.textContent)
      .toContain('Static HTML');
  });
});
