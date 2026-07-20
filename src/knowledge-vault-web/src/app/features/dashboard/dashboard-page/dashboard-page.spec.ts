import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { DashboardPage } from './dashboard-page';

describe('DashboardPage', () => {
  it('shows project activity controls and an English Knowledge Hub without recent knowledge', async () => {
    const now = new Date();
    const dateKey = [
      now.getFullYear(),
      String(now.getMonth() + 1).padStart(2, '0'),
      String(now.getDate()).padStart(2, '0'),
    ].join('-');
    const api = {
      getProjectDocumentStats: vi
        .fn()
        .mockReturnValue(of({ documentCount: 12, categoryCount: 4, tagCount: 5 })),
      listProjects: vi.fn().mockReturnValue(
        of({
          items: [
            {
              id: 'project-1',
              name: 'Atlas',
              isArchived: false,
              currentUserRole: 'Editor',
              isFollowing: true,
              memberCount: 2,
              createdAt: now.toISOString(),
            },
          ],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
      listProjectDocumentActivity: vi
        .fn()
        .mockReturnValue(of([{ date: dateKey, changeCount: 3 }])),
    };

    await TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [provideRouter([]), { provide: ApiClient, useValue: api }],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Project document changes');
    expect(element.textContent).toContain('Project documents');
    expect(element.textContent).toContain('Used by project documents');
    expect(element.textContent).toContain('All followed projects');
    expect(element.textContent).not.toContain('Recent knowledge');
    expect(element.querySelectorAll('.bar').length).toBe(7);

    fixture.componentInstance.activityError.set('Request failed with status 404.');
    fixture.detectChanges();
    expect(element.querySelector('.activity-error')).toBeTruthy();

    const hubTab = Array.from(element.querySelectorAll('.dashboard-tabs button')).find(
      (button) => button.textContent?.trim() === 'Knowledge Hub',
    ) as HTMLButtonElement;
    hubTab.click();
    fixture.detectChanges();

    const githubLink = element.querySelector('.github-link') as HTMLAnchorElement;
    expect(element.textContent).toContain('How to use KnowledgeVault');
    expect(element.querySelector('.activity-error')).toBeNull();
    expect(githubLink.href).toBe('https://github.com/Jzhong2026/KnowledgeVault');
  });
});
