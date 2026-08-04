import { Injectable, signal } from '@angular/core';
import { Subject } from 'rxjs';

/**
 * Shape of a confirmation request. Pass it to {@link ConfirmService.confirm}
 * to display the system-styled dialog and await the user's choice.
 */
export interface ConfirmOptions {
  title: string;
  message: string;
  /** Label of the confirm (positive) action. Defaults to "Confirm". */
  confirmLabel?: string;
  /** Label of the cancel (negative) action. Defaults to "Cancel". */
  cancelLabel?: string;
  /** Visual style for the confirm button. Defaults to "primary". */
  intent?: 'primary' | 'danger';
}

export interface ConfirmRequest extends ConfirmOptions {
  readonly id: number;
  readonly resolver: (value: boolean) => void;
}

/**
 * Imperative service that exposes a Promise-returning `confirm()` and renders
 * a global ConfirmDialog at the application root. Use this anywhere instead of
 * the native `window.confirm` so the visual style matches the rest of the
 * KnowledgeVault UI and so the dialog is keyboard-accessible.
 *
 * The actual dialog markup lives in {@link ConfirmDialogHost}, which is
 * mounted once by the application root component.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly _requests = signal<ConfirmRequest | null>(null);
  /** Stream of pending requests; the host component subscribes to render
   *  the dialog without us having to inject the component tree here. */
  readonly requests = new Subject<ConfirmRequest | null>();
  private nextId = 0;

  /**
   * Display the confirm dialog and resolve with the user's choice.
   * Resolves to `true` when the user confirms, `false` when they cancel or
   * dismiss the dialog via the backdrop / Escape / X button.
   */
  confirm(options: ConfirmOptions): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      const request: ConfirmRequest = {
        ...options,
        id: ++this.nextId,
        resolver: resolve,
      };
      this._requests.set(request);
      this.requests.next(request);
    });
  }

  /** Used by the host component to read the active request. */
  takeActive(): ConfirmRequest | null {
    return this._requests();
  }

  /** Called by the host component once it has rendered or dismissed. */
  resolveCurrent(value: boolean): void {
    const active = this._requests();
    if (!active) {
      return;
    }
    this._requests.set(null);
    this.requests.next(null);
    active.resolver(value);
  }
}