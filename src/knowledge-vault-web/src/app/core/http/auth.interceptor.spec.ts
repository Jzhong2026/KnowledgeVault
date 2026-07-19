import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  const storageKey = 'knowledge-vault.auth';
  const router = {
    navigate: vi.fn().mockResolvedValue(true),
  };

  let http: HttpClient;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    localStorage.setItem(
      storageKey,
      JSON.stringify({
        token: 'expired-token',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: {
          id: 'user-id',
          userName: 'test-user',
          email: 'test@example.com',
          createdAt: new Date().toISOString(),
        },
      }),
    );

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: router },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.removeItem(storageKey);
    router.navigate.mockClear();
  });

  it('clears the session and redirects to login when an API request returns 401', () => {
    http.get('/KnowledgeVault/api/projects').subscribe({ error: () => undefined });

    const request = httpTesting.expectOne('/KnowledgeVault/api/projects');
    expect(request.request.headers.get('Authorization')).toBe('Bearer expired-token');

    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(localStorage.getItem(storageKey)).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/auth'], { replaceUrl: true });
  });
});
