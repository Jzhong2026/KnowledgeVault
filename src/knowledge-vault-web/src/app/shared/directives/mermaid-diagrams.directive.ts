import { isPlatformBrowser } from '@angular/common';
import { Directive, ElementRef, inject, Input, OnDestroy, PLATFORM_ID } from '@angular/core';

let diagramId = 0;
let mermaidModule: Promise<typeof import('mermaid')> | undefined;

@Directive({
  selector: '[appMermaidDiagrams]',
})
export class MermaidDiagramsDirective implements OnDestroy {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private renderVersion = 0;

  @Input()
  set appMermaidDiagrams(_content: string | null | undefined) {
    const version = ++this.renderVersion;

    if (this.isBrowser) {
      queueMicrotask(() => void this.renderDiagrams(version));
    }
  }

  ngOnDestroy(): void {
    this.renderVersion++;
  }

  private async renderDiagrams(version: number): Promise<void> {
    const diagrams = Array.from(
      this.element.nativeElement.querySelectorAll<HTMLElement>('.mermaid-diagram'),
    );

    if (!diagrams.length) {
      return;
    }

    const mermaid = (await loadMermaid()).default;

    if (version !== this.renderVersion) {
      return;
    }

    for (const diagram of diagrams) {
      const source = diagram.querySelector('.mermaid-source')?.textContent?.trim();

      if (!source) {
        continue;
      }

      try {
        const id = `mermaid-diagram-${++diagramId}`;
        const { svg, bindFunctions } = await mermaid.render(id, source);

        if (version !== this.renderVersion || !diagram.isConnected) {
          return;
        }

        diagram.innerHTML = svg;
        diagram.setAttribute('role', 'img');
        diagram.setAttribute('aria-label', 'Mermaid diagram');
        bindFunctions?.(diagram);
      } catch {
        diagram.classList.add('mermaid-diagram--error');

        const message = document.createElement('p');
        message.className = 'mermaid-error';
        message.textContent = 'Mermaid diagram could not be rendered. Check the diagram syntax.';
        diagram.append(message);
      }
    }
  }
}

async function loadMermaid(): Promise<typeof import('mermaid')> {
  mermaidModule ??= import('mermaid').then((module) => {
    module.default.initialize({
      startOnLoad: false,
      securityLevel: 'strict',
      theme: 'neutral',
    });

    return module;
  });

  return mermaidModule;
}
