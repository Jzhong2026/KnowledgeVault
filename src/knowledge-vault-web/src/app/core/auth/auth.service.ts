import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';

import { API_BASE_URL } from '../config/api.config';
import { AuthResponse, AuthState, LoginRequest, RegisterRequest } from '../models/auth.models';

const storageKey = 'knowledge-vault.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly state = signal<AuthState>(this.readState());

  readonly currentUser = computed(() => this.state().user);
  readonly token = computed(() => this.state().token);
  readonly isAuthenticated = computed(() => {
    const state = this.state();
    return !!state.token && !!state.expiresAt && new Date(state.expiresAt).getTime() > Date.now();
  });

  register(request: RegisterRequest) {
    return this.http.post<AuthResponse>(`${this.baseUrl}/auth/register`, request).pipe(
      tap((response) => this.setAuth(response)),
    );
  }

  login(request: LoginRequest) {
    return this.http.post<AuthResponse>(`${this.baseUrl}/auth/login`, request).pipe(
      tap((response) => this.setAuth(response)),
    );
  }

  logout(): void {
    localStorage.removeItem(storageKey);
    this.state.set({ token: null, expiresAt: null, user: null });
  }

  refreshMe() {
    return this.http.get<AuthResponse['user']>(`${this.baseUrl}/auth/me`).pipe(
      tap((user) => {
        this.state.update((state) => ({ ...state, user }));
        this.writeState(this.state());
      }),
    );
  }

  private setAuth(response: AuthResponse): void {
    const nextState: AuthState = {
      token: response.accessToken,
      expiresAt: response.expiresAt,
      user: response.user,
    };
    this.state.set(nextState);
    this.writeState(nextState);
  }

  private readState(): AuthState {
    if (typeof localStorage === 'undefined') {
      return { token: null, expiresAt: null, user: null };
    }

    const value = localStorage.getItem(storageKey);
    if (!value) {
      return { token: null, expiresAt: null, user: null };
    }

    try {
      return JSON.parse(value) as AuthState;
    } catch {
      localStorage.removeItem(storageKey);
      return { token: null, expiresAt: null, user: null };
    }
  }

  private writeState(state: AuthState): void {
    localStorage.setItem(storageKey, JSON.stringify(state));
  }
}
