import { TestBed } from '@angular/core/testing';

import { ConfirmService } from './confirm-dialog';
import { ConfirmDialogHost } from './confirm-dialog-host';

describe('ConfirmService', () => {
  it('resolves true when the user confirms and false on cancel', async () => {
    TestBed.configureTestingModule({ imports: [ConfirmDialogHost] });
    const service = TestBed.inject(ConfirmService);
    const fixture = TestBed.createComponent(ConfirmDialogHost);

    const confirmPromise = service.confirm({ title: 'Heads up', message: 'Proceed?' });
    fixture.detectChanges();
    expect(fixture.componentInstance.request()).not.toBeNull();
    fixture.componentInstance.onConfirm();
    await expect(confirmPromise).resolves.toBe(true);
    expect(fixture.componentInstance.request()).toBeNull();

    const denyPromise = service.confirm({ title: 'Heads up', message: 'Proceed?' });
    fixture.detectChanges();
    fixture.componentInstance.onCancel();
    await expect(denyPromise).resolves.toBe(false);
  });

  it('treats backdrop clicks as cancel', async () => {
    TestBed.configureTestingModule({ imports: [ConfirmDialogHost] });
    const service = TestBed.inject(ConfirmService);
    const fixture = TestBed.createComponent(ConfirmDialogHost);

    const promise = service.confirm({ title: 'T', message: 'M' });
    fixture.detectChanges();
    fixture.componentInstance.onBackdrop();
    await expect(promise).resolves.toBe(false);
  });

  it('exposes default labels and intent when none are supplied', () => {
    TestBed.configureTestingModule({ imports: [ConfirmDialogHost] });
    const service = TestBed.inject(ConfirmService);
    service.confirm({ title: 'T', message: 'M' });
    const active = service.takeActive();
    expect(active?.confirmLabel).toBeUndefined();
    expect(active?.cancelLabel).toBeUndefined();
    expect(active?.intent ?? 'primary').toBe('primary');
  });

  it('forwards danger intent to the host button class', () => {
    TestBed.configureTestingModule({ imports: [ConfirmDialogHost] });
    const service = TestBed.inject(ConfirmService);
    service.confirm({ title: 'T', message: 'M', intent: 'danger' });
    const active = service.takeActive();
    expect(active?.intent).toBe('danger');
  });
});