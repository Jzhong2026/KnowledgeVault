import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { ConfirmService } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { ConfirmDialogHost } from '../../../../shared/components/confirm-dialog/confirm-dialog-host';

import { ContentEditor } from './content-editor';

describe('ContentEditor', () => {
  it('renders a live Markdown preview on the left of the source editor', async () => {
    await TestBed.configureTestingModule({ imports: [ContentEditor] }).compileComponents();

    const fixture = TestBed.createComponent(ContentEditor);
    fixture.componentRef.setInput('content', '# Original');
    fixture.detectChanges();

    const panes = fixture.nativeElement.querySelector('.content-editor__panes') as HTMLElement;
    expect(panes.firstElementChild?.classList.contains('content-editor__pane--preview')).toBe(true);

    const source = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    source.value = '# Updated';
    source.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.content-editor__preview h1')?.textContent).toBe('Updated');
  });

  it('asks via ConfirmService before discarding changed content', async () => {
    await TestBed.configureTestingModule({
      imports: [ContentEditor, ConfirmDialogHost],
    }).compileComponents();

    const confirmService = TestBed.inject(ConfirmService);
    const hostFixture = TestBed.createComponent(ConfirmDialogHost);
    hostFixture.detectChanges();

    const fixture = TestBed.createComponent(ContentEditor);
    fixture.componentRef.setInput('content', 'Original');
    fixture.detectChanges();
    fixture.componentInstance.draft = 'Changed';
    const cancel = vi.fn();
    fixture.componentInstance.cancelEdit.subscribe(cancel);

    const request = firstValueFrom(confirmService.requests);
    const closePromise = fixture.componentInstance.requestClose();
    await Promise.resolve();

    const active = confirmService.takeActive();
    expect(active).not.toBeNull();
    expect(active?.title).toBe('Discard changes?');
    // Simulate the user cancelling.
    confirmService.resolveCurrent(false);
    await closePromise;
    expect(cancel).not.toHaveBeenCalled();

    const request2 = firstValueFrom(confirmService.requests);
    const closePromise2 = fixture.componentInstance.requestClose();
    await Promise.resolve();
    expect(confirmService.takeActive()).not.toBeNull();
    confirmService.resolveCurrent(true);
    await closePromise2;
    expect(cancel).toHaveBeenCalledOnce();
    void request;
    void request2;
  });

  it('closes immediately when there are no unsaved edits', async () => {
    await TestBed.configureTestingModule({
      imports: [ContentEditor, ConfirmDialogHost],
    }).compileComponents();

    const confirmService = TestBed.inject(ConfirmService);
    const hostFixture = TestBed.createComponent(ConfirmDialogHost);
    hostFixture.detectChanges();

    const fixture = TestBed.createComponent(ContentEditor);
    fixture.componentRef.setInput('content', 'same');
    fixture.detectChanges();
    const cancel = vi.fn();
    fixture.componentInstance.cancelEdit.subscribe(cancel);

    await fixture.componentInstance.requestClose();
    expect(confirmService.takeActive()).toBeNull();
    expect(cancel).toHaveBeenCalledOnce();
  });

  it('wires scroll handlers on both source textarea and preview pane', async () => {
    await TestBed.configureTestingModule({ imports: [ContentEditor] }).compileComponents();

    const fixture = TestBed.createComponent(ContentEditor);
    fixture.componentRef.setInput('content', '# Title\n\nbody');
    fixture.detectChanges();

    const previewEl = fixture.nativeElement.querySelector('.content-editor__preview') as HTMLElement;
    const sourceEl = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;

    expect(previewEl).toBeTruthy();
    expect(sourceEl).toBeTruthy();
    expect(sourceEl.getAttribute('aria-label')).toBe('Markdown source');

    // Just verify that scroll handlers don't throw and ignore state when the
    // panes haven't been laid out yet (jsdom can't simulate real scroll geometry).
    expect(() => sourceEl.dispatchEvent(new Event('scroll'))).not.toThrow();
  });

  it('scrolls the preview near the caret after appending content at the end', async () => {
    await TestBed.configureTestingModule({ imports: [ContentEditor] }).compileComponents();

    const fixture = TestBed.createComponent(ContentEditor);
    fixture.componentRef.setInput('content', 'line 1\nline 2\nline 3');
    fixture.detectChanges();

    const component = fixture.componentInstance;
    const previewEl = component['previewEl']?.nativeElement as HTMLElement;
    const sourceEl = component['sourceEl']?.nativeElement as HTMLTextAreaElement;
    Object.defineProperty(previewEl, 'scrollHeight', { configurable: true, value: 1000 });
    Object.defineProperty(previewEl, 'clientHeight', { configurable: true, value: 100 });
    Object.defineProperty(sourceEl, 'selectionStart', { configurable: true, value: 0, writable: true });

    sourceEl.value = `${sourceEl.value}\nline 4 appended at the end`;
    sourceEl.selectionStart = sourceEl.value.length;
    component.onDraftInput(sourceEl.value);
    await Promise.resolve();

    expect(previewEl.scrollTop).toBeGreaterThan(0);
  });

  it('moves the preview near the caret when the user clicks lower in the source', async () => {
    await TestBed.configureTestingModule({ imports: [ContentEditor] }).compileComponents();

    const fixture = TestBed.createComponent(ContentEditor);
    fixture.componentRef.setInput('content', 'one\ntwo\nthree\nfour\nfive');
    fixture.detectChanges();

    const component = fixture.componentInstance;
    const previewEl = component['previewEl']?.nativeElement as HTMLElement;
    const sourceEl = component['sourceEl']?.nativeElement as HTMLTextAreaElement;
    Object.defineProperty(previewEl, 'scrollHeight', { configurable: true, value: 1600 });
    Object.defineProperty(previewEl, 'clientHeight', { configurable: true, value: 200 });
    Object.defineProperty(sourceEl, 'selectionStart', { configurable: true, value: 0, writable: true });

    sourceEl.value = component.draft;
    sourceEl.selectionStart = sourceEl.value.length - 2;
    component.syncPreviewToCaret();
    await Promise.resolve();

    expect(previewEl.scrollTop).toBeGreaterThan(0);
  });
});