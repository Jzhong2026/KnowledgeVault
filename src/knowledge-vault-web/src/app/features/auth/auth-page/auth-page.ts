import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

type AuthMode = 'login' | 'register';

@Component({
  selector: 'app-auth-page',
  imports: [ReactiveFormsModule, LoadingIndicator],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.css',
})
export class AuthPage {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly mode = signal<AuthMode>('login');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    userName: [''],
    email: ['', [Validators.email]],
    userNameOrEmail: ['', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  setMode(mode: AuthMode): void {
    this.mode.set(mode);
    this.error.set(null);

    if (mode === 'register') {
      this.form.controls.userName.setValidators([Validators.required, Validators.maxLength(64)]);
      this.form.controls.email.setValidators([Validators.required, Validators.email, Validators.maxLength(256)]);
      this.form.controls.userNameOrEmail.clearValidators();
    } else {
      this.form.controls.userName.clearValidators();
      this.form.controls.email.setValidators([Validators.email]);
      this.form.controls.userNameOrEmail.setValidators([Validators.required]);
    }

    this.form.controls.userName.updateValueAndValidity();
    this.form.controls.email.updateValueAndValidity();
    this.form.controls.userNameOrEmail.updateValueAndValidity();
  }

  submit(): void {
    this.setMode(this.mode());

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    const request = this.mode() === 'register'
      ? this.authService.register({
          userName: value.userName,
          email: value.email,
          password: value.password,
        })
      : this.authService.login({
          userNameOrEmail: value.userNameOrEmail,
          password: value.password,
        });

    request.subscribe({
      next: () => void this.router.navigate(['/dashboard']),
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
      complete: () => this.loading.set(false),
    });
  }
}
