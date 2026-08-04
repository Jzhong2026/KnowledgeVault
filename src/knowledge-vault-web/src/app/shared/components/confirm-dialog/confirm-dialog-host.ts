import { Component, HostListener, inject, signal } from '@angular/core';

import { ConfirmService } from './confirm-dialog';

/**
 * Mount this component once at the application root (see
 * `app.html`). It listens to {@link ConfirmService} and renders the active
 * confirmation dialog with the same styling as the rest of the workspace.
 *
 * The dialog itself never lives inside any feature component, so it stays
 * visible above modals / popovers / full-screen editors and never disappears
 * when the calling component is destroyed mid-await.
 */
@Component({
  selector: 'app-confirm-dialog-host',
  template: `
    @if (request(); as active) {
      <div class="confirm-dialog-backdrop" (click)="onBackdrop()">
        <section
          class="confirm-dialog"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="confirm-dialog-title-{{ active.id }}"
          aria-describedby="confirm-dialog-message-{{ active.id }}"
          (click)="$event.stopPropagation()"
        >
          <header class="confirm-dialog__head">
            <h2 id="confirm-dialog-title-{{ active.id }}">{{ active.title }}</h2>
            <button
              type="button"
              class="confirm-dialog__close"
              aria-label="Close confirmation dialog"
              (click)="onCancel()"
            >
              ×
            </button>
          </header>
          <p id="confirm-dialog-message-{{ active.id }}" class="confirm-dialog__message">
            {{ active.message }}
          </p>
          <div class="confirm-dialog__actions">
            <button type="button" class="ghost" (click)="onCancel()">
              {{ active.cancelLabel || 'Cancel' }}
            </button>
            <button
              type="button"
              [class]="active.intent === 'danger' ? 'danger' : 'primary'"
              (click)="onConfirm()"
            >
              {{ active.confirmLabel || 'Confirm' }}
            </button>
          </div>
        </section>
      </div>
    }
  `,
  styleUrl: './confirm-dialog.css',
})
export class ConfirmDialogHost {
  private readonly confirm = inject(ConfirmService);
  readonly request = signal(this.confirm.takeActive());

  constructor() {
    this.confirm.requests.subscribe((req) => this.request.set(req));
  }

  onConfirm(): void {
    this.confirm.resolveCurrent(true);
  }

  onCancel(): void {
    this.confirm.resolveCurrent(false);
  }

  onBackdrop(): void {
    this.confirm.resolveCurrent(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.request()) {
      this.confirm.resolveCurrent(false);
    }
  }
}