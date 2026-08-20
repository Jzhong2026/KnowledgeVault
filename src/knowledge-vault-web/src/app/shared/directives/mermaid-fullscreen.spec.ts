import { closeMermaidFullscreen, createMermaidDiagramToolbar } from './mermaid-fullscreen';

describe('mermaid fullscreen viewer', () => {
  afterEach(() => {
    closeMermaidFullscreen();
  });

  it('creates a floating toolbar with icon-only zoom, download and copy actions', () => {
    const toolbar = createMermaidDiagramToolbar('<svg></svg>', 'flowchart LR\n  A --> B', 7);
    const buttons = Array.from(toolbar.querySelectorAll('button'));

    expect(toolbar.className).toBe('mermaid-diagram__toolbar');
    expect(buttons.length).toBe(3);
    expect(buttons[0]?.getAttribute('aria-label')).toContain('放大查看');
    expect(buttons[1]?.getAttribute('aria-label')).toContain('下载 SVG');
    expect(buttons[2]?.getAttribute('aria-label')).toContain('复制源码');
    expect(buttons[0]?.querySelector('svg')).not.toBeNull();
  });

  it('opens the fullscreen overlay with the diagram when zoom is clicked', () => {
    const toolbar = createMermaidDiagramToolbar(
      '<svg width="400" height="200"></svg>',
      'flowchart LR',
      7,
    );

    (toolbar.querySelectorAll('button')[0] as HTMLElement).click();

    const overlay = document.querySelector('.mermaid-fullscreen');
    expect(overlay).not.toBeNull();
    expect(overlay?.getAttribute('role')).toBe('dialog');
    expect(overlay?.querySelector('.mermaid-fullscreen__canvas svg')).not.toBeNull();
    expect(document.body.style.overflow).toBe('hidden');
  });

  it('closes the overlay via the close button and restores the page scroll', () => {
    const toolbar = createMermaidDiagramToolbar('<svg></svg>', 'flowchart LR', 7);
    (toolbar.querySelectorAll('button')[0] as HTMLElement).click();

    const closeButton = document.querySelector<HTMLButtonElement>(
      '.mermaid-fullscreen__button--close',
    );
    closeButton?.click();

    expect(document.querySelector('.mermaid-fullscreen')).toBeNull();
    expect(document.body.style.overflow).not.toBe('hidden');
  });

  it('closes the overlay on Escape', () => {
    const toolbar = createMermaidDiagramToolbar('<svg></svg>', 'flowchart LR', 7);
    (toolbar.querySelectorAll('button')[0] as HTMLElement).click();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(document.querySelector('.mermaid-fullscreen')).toBeNull();
  });

  it('replaces the previous overlay instead of stacking viewers', () => {
    const toolbar = createMermaidDiagramToolbar('<svg></svg>', 'flowchart LR', 7);
    (toolbar.querySelectorAll('button')[0] as HTMLElement).click();
    (toolbar.querySelectorAll('button')[0] as HTMLElement).click();

    expect(document.querySelectorAll('.mermaid-fullscreen').length).toBe(1);
  });
});
