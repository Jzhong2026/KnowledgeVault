/**
 * Fullscreen viewer and toolbar actions for rendered Mermaid diagrams.
 *
 * The viewer is intentionally framework-free (plain DOM) so the directive can
 * mount it on `document.body` regardless of where the diagram lives. The
 * Markdown source is never modified - every node here is runtime-injected.
 */

const ICONS = {
  zoom: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 3h6v6"/><path d="M3 15v6h6"/><path d="M21 3l-7 7"/><path d="M3 21l7-7"/></svg>',
  download: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="3" x2="12" y2="15"/></svg>',
  copy: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>',
  close: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 6L6 18"/><path d="M6 6l12 12"/></svg>',
  zoomIn: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/><line x1="11" y1="8" x2="11" y2="14"/><line x1="8" y1="11" x2="14" y2="11"/></svg>',
  zoomOut: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/><line x1="8" y1="11" x2="14" y2="11"/></svg>',
  fit: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8 3H5a2 2 0 0 0-2 2v3"/><path d="M21 8V5a2 2 0 0 0-2-2h-3"/><path d="M3 16v3a2 2 0 0 0 2 2h3"/><path d="M16 21h3a2 2 0 0 0 2-2v-3"/></svg>',
};

const CANVAS_PADDING = 28;
const MIN_SCALE = 0.05;
const MAX_SCALE = 10;
const ZOOM_STEP = 1.25;

let activeOverlay: HTMLElement | null = null;
let activeCleanup: (() => void) | null = null;

/** Floating toolbar on top-right of each Mermaid diagram: zoom / download / copy source. */
export function createMermaidDiagramToolbar(
  svgMarkup: string,
  source: string,
  diagramId: number,
): HTMLElement {
  const toolbar = document.createElement('div');
  toolbar.className = 'mermaid-diagram__toolbar';
  toolbar.setAttribute('role', 'toolbar');
  toolbar.setAttribute('aria-label', 'Mermaid diagram actions');

  const actions = [
    {
      label: '放大查看',
      icon: ICONS.zoom,
      run: () => openMermaidFullscreen(svgMarkup, source, diagramId),
    },
    {
      label: '下载 SVG',
      icon: ICONS.download,
      run: () => downloadMermaidSvg(svgMarkup, diagramId),
    },
    {
      label: '复制源码',
      icon: ICONS.copy,
      run: (event: MouseEvent) => void copyMermaidSource(source, event.currentTarget),
    },
  ];

  for (const action of actions) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'mermaid-diagram__action';
    button.title = action.label;
    button.setAttribute('aria-label', action.label);
    button.innerHTML = action.icon;
    button.addEventListener('click', action.run);
    toolbar.append(button);
  }

  return toolbar;
}

export function downloadMermaidSvg(svgMarkup: string, diagramId: number): void {
  const blob = new Blob([`<?xml version="1.0" encoding="UTF-8"?>\n${svgMarkup}`], {
    type: 'image/svg+xml;charset=utf-8',
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `mermaid-diagram-${diagramId}.svg`;
  link.click();
  URL.revokeObjectURL(url);
}

export function openMermaidFullscreen(svgMarkup: string, source: string, diagramId: number): void {
  if (typeof document === 'undefined') {
    return;
  }

  closeMermaidFullscreen();

  const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  const previousOverflow = document.body.style.overflow;
  document.body.style.overflow = 'hidden';

  const overlay = document.createElement('div');
  overlay.className = 'mermaid-fullscreen';
  overlay.setAttribute('role', 'dialog');
  overlay.setAttribute('aria-modal', 'true');
  overlay.setAttribute('aria-label', `Mermaid diagram ${diagramId} viewer`);

  const title = document.createElement('span');
  title.className = 'mermaid-fullscreen__title';
  title.textContent = `Mermaid 图 #${diagramId}`;

  const zoomLabel = document.createElement('span');
  zoomLabel.className = 'mermaid-fullscreen__zoom-label';
  zoomLabel.setAttribute('aria-live', 'polite');
  zoomLabel.textContent = '100%';

  const controls = document.createElement('div');
  controls.className = 'mermaid-fullscreen__controls';

  const zoomOutButton = createControlButton('缩小', ICONS.zoomOut);
  const zoomInButton = createControlButton('放大', ICONS.zoomIn);
  const fitButton = createControlButton('适应窗口', ICONS.fit);
  const actualButton = createControlButton('原始大小', '<span class="mermaid-fullscreen__ratio">1:1</span>');
  const downloadButton = createControlButton('下载 SVG', ICONS.download);
  const copyButton = createControlButton('复制源码', ICONS.copy);
  const closeButton = createControlButton('关闭', ICONS.close, 'mermaid-fullscreen__button--close');

  controls.append(
    zoomOutButton,
    zoomLabel,
    zoomInButton,
    createControlDivider(),
    fitButton,
    actualButton,
    createControlDivider(),
    downloadButton,
    copyButton,
    closeButton,
  );

  const header = document.createElement('div');
  header.className = 'mermaid-fullscreen__header';
  header.append(title, controls);

  const canvas = document.createElement('div');
  canvas.className = 'mermaid-fullscreen__canvas';
  canvas.innerHTML = svgMarkup;

  const viewport = document.createElement('div');
  viewport.className = 'mermaid-fullscreen__viewport';
  viewport.setAttribute('tabindex', '0');
  viewport.append(canvas);

  overlay.append(header, viewport);
  document.body.append(overlay);
  activeOverlay = overlay;

  const svg = canvas.querySelector('svg');
  const baseSize = svg ? readSvgSize(svg) : { width: 800, height: 600 };
  const state = {
    scale: 1,
    fitMode: true,
    fitScale: 1,
  };

  const computeFitScale = (): number => {
    const availableWidth = viewport.clientWidth - CANVAS_PADDING * 2;
    const availableHeight = viewport.clientHeight - CANVAS_PADDING * 2;

    if (availableWidth <= 0 || availableHeight <= 0 || baseSize.width <= 0 || baseSize.height <= 0) {
      return 1;
    }

    return Math.min(availableWidth / baseSize.width, availableHeight / baseSize.height, 1);
  };

  const renderScale = (): void => {
    zoomLabel.textContent = `${Math.round(state.scale * 100)}%`;
  };

  const applyScale = (scale: number): void => {
    state.scale = scale;

    if (svg) {
      svg.style.maxWidth = 'none';
      svg.style.width = `${baseSize.width * scale}px`;
      svg.style.height = `${baseSize.height * scale}px`;
    }

    renderScale();
  };

  const enterFitMode = (): void => {
    state.fitMode = true;
    state.fitScale = computeFitScale();
    applyScale(state.fitScale);
  };

  const setScale = (scale: number): void => {
    state.fitMode = false;
    applyScale(clamp(scale, MIN_SCALE, MAX_SCALE));
  };

  const zoomAt = (anchorX: number, anchorY: number, factor: number): void => {
    const nextScale = clamp(state.scale * factor, MIN_SCALE, MAX_SCALE);

    if (nextScale === state.scale) {
      return;
    }

    const previousScale = state.scale;
    const viewportRect = viewport.getBoundingClientRect();
    const anchorLeft = anchorX - viewportRect.left;
    const anchorTop = anchorY - viewportRect.top;
    const contentX = anchorLeft + viewport.scrollLeft;
    const contentY = anchorTop + viewport.scrollTop;

    setScale(nextScale);

    // Keep the content under the cursor anchored while zooming.
    const ratio = nextScale / previousScale;
    viewport.scrollLeft = contentX * ratio - anchorLeft;
    viewport.scrollTop = contentY * ratio - anchorTop;
  };

  const zoomFromControl = (factor: number): void => {
    const viewportRect = viewport.getBoundingClientRect();
    zoomAt(
      viewportRect.left + viewportRect.width / 2,
      viewportRect.top + viewportRect.height / 2,
      factor,
    );
  };

  const onWheel = (event: WheelEvent): void => {
    // Plain wheel zooms toward the cursor. Shift+wheel still scrolls horizontally.
    const zoom =
      !event.shiftKey && Math.abs(event.deltaX) < Math.abs(event.deltaY);

    if (!zoom) {
      return; // Let the browser natively scroll the viewport.
    }

    event.preventDefault();
    zoomAt(event.clientX, event.clientY, event.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP);
  };

  let dragging = false;
  let dragMoved = false;
  let dragStartX = 0;
  let dragStartY = 0;
  let dragStartScrollLeft = 0;
  let dragStartScrollTop = 0;

  const onPointerDown = (event: PointerEvent): void => {
    if (event.button !== 0) {
      return;
    }

    dragging = true;
    dragMoved = false;
    dragStartX = event.clientX;
    dragStartY = event.clientY;
    dragStartScrollLeft = viewport.scrollLeft;
    dragStartScrollTop = viewport.scrollTop;
    viewport.classList.add('mermaid-fullscreen__viewport--dragging');

    try {
      viewport.setPointerCapture(event.pointerId);
    } catch {
      // Pointer capture is unavailable in some environments (e.g. jsdom).
    }
  };

  const onPointerMove = (event: PointerEvent): void => {
    if (!dragging) {
      return;
    }

    const deltaX = event.clientX - dragStartX;
    const deltaY = event.clientY - dragStartY;

    if (Math.abs(deltaX) > 4 || Math.abs(deltaY) > 4) {
      dragMoved = true;
    }

    viewport.scrollLeft = dragStartScrollLeft - deltaX;
    viewport.scrollTop = dragStartScrollTop - deltaY;
  };

  const endDrag = (event: PointerEvent): void => {
    if (!dragging) {
      return;
    }

    dragging = false;
    viewport.classList.remove('mermaid-fullscreen__viewport--dragging');

    try {
      if (viewport.hasPointerCapture(event.pointerId)) {
        viewport.releasePointerCapture(event.pointerId);
      }
    } catch {
      // Ignore unsupported pointer capture APIs.
    }
  };

  const onViewportClick = (event: MouseEvent): void => {
    if (dragMoved) {
      return; // Ignore the click that ends a pan gesture.
    }

    if (event.target === viewport || event.target === canvas) {
      closeMermaidFullscreen();
    }
  };

  const onDoubleClick = (): void => {
    if (state.fitMode) {
      setScale(1);
    } else {
      enterFitMode();
    }
  };

  const onKeydown = (event: KeyboardEvent): void => {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeMermaidFullscreen();
      return;
    }

    if (event.key === '+' || event.key === '=') {
      zoomFromControl(ZOOM_STEP);
    } else if (event.key === '-') {
      zoomFromControl(1 / ZOOM_STEP);
    } else if (event.key === '0') {
      setScale(1);
    }
  };

  const onResize = (): void => {
    if (state.fitMode) {
      enterFitMode();
    } else {
      state.fitScale = computeFitScale();
    }
  };

  zoomOutButton.addEventListener('click', () => zoomFromControl(1 / ZOOM_STEP));
  zoomInButton.addEventListener('click', () => zoomFromControl(ZOOM_STEP));
  fitButton.addEventListener('click', enterFitMode);
  actualButton.addEventListener('click', () => setScale(1));
  downloadButton.addEventListener('click', () => downloadMermaidSvg(svgMarkup, diagramId));
  copyButton.addEventListener('click', (event) =>
    void copyMermaidSource(source, event.currentTarget),
  );
  closeButton.addEventListener('click', closeMermaidFullscreen);

  viewport.addEventListener('wheel', onWheel, { passive: false });
  viewport.addEventListener('pointerdown', onPointerDown);
  viewport.addEventListener('pointermove', onPointerMove);
  viewport.addEventListener('pointerup', endDrag);
  viewport.addEventListener('pointercancel', endDrag);
  viewport.addEventListener('click', onViewportClick);
  viewport.addEventListener('dblclick', onDoubleClick);
  document.addEventListener('keydown', onKeydown);
  window.addEventListener('resize', onResize);

  enterFitMode();
  closeButton.focus();

  activeCleanup = () => {
    viewport.removeEventListener('wheel', onWheel);
    viewport.removeEventListener('pointerdown', onPointerDown);
    viewport.removeEventListener('pointermove', onPointerMove);
    viewport.removeEventListener('pointerup', endDrag);
    viewport.removeEventListener('pointercancel', endDrag);
    viewport.removeEventListener('click', onViewportClick);
    viewport.removeEventListener('dblclick', onDoubleClick);
    document.removeEventListener('keydown', onKeydown);
    window.removeEventListener('resize', onResize);

    document.body.style.overflow = previousOverflow;
    overlay.remove();
    activeOverlay = null;
    activeCleanup = null;
    previouslyFocused?.focus();
  };
}

export function closeMermaidFullscreen(): void {
  activeCleanup?.();
}

async function copyMermaidSource(source: string, target: EventTarget | null): Promise<void> {
  const button = target instanceof HTMLButtonElement ? target : null;
  const copied = await writeTextToClipboard(source);

  if (button) {
    const previous = button.title;
    const message = copied ? '已复制' : '复制失败';
    button.title = message;
    button.classList.add(copied ? 'mermaid-diagram__action--done' : 'mermaid-diagram__action--error');
    window.setTimeout(() => {
      button.title = previous;
      button.classList.remove('mermaid-diagram__action--done', 'mermaid-diagram__action--error');
    }, 1600);
  }
}

async function writeTextToClipboard(text: string): Promise<boolean> {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // Fall through to the legacy path (non-secure contexts).
    }
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.setAttribute('readonly', '');
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.append(textarea);
  textarea.select();

  let copied = false;

  try {
    copied = document.execCommand('copy');
  } catch {
    copied = false;
  }

  textarea.remove();
  return copied;
}

function createControlButton(
  label: string,
  content: string,
  extraClass = '',
): HTMLButtonElement {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = `mermaid-fullscreen__button${extraClass ? ` ${extraClass}` : ''}`;
  button.title = label;
  button.setAttribute('aria-label', label);
  button.innerHTML = content;
  return button;
}

function createControlDivider(): HTMLElement {
  const divider = document.createElement('span');
  divider.className = 'mermaid-fullscreen__divider';
  divider.setAttribute('aria-hidden', 'true');
  return divider;
}

function readSvgSize(svg: SVGSVGElement): { width: number; height: number } {
  // Mermaid-generated SVGs carry a reliable viewBox; prefer that over width/height
  // attributes which are often percentages or absent.
  const box = svg.viewBox?.baseVal;
  if (box && box.width > 0 && box.height > 0) {
    return { width: box.width, height: box.height };
  }

  const width =
    parseCssPixels(svg.getAttribute('width')) ||
    svg.clientWidth ||
    svg.getBoundingClientRect().width ||
    800;
  const height =
    parseCssPixels(svg.getAttribute('height')) ||
    svg.clientHeight ||
    svg.getBoundingClientRect().height ||
    600;

  return { width, height };
}

function parseCssPixels(value: string | null | undefined): number {
  if (!value) {
    return 0;
  }

  const match = /^([\d.]+)px$/i.exec(value.trim());
  return match ? Number.parseFloat(match[1]) : 0;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
