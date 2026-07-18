import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { KnowledgeItem, RevisionSummary } from '../../../core/models/knowledge.models';
import { KnowledgeDetailPage } from './knowledge-detail-page';

describe('KnowledgeDetailPage', () => {
  it('places revisions in a right rail and comments in a full-width row', async () => {
    const item: KnowledgeItem = {
      id: 'document-id',
      scope: 'Project',
      projectId: 'project-id',
      ownerUserId: 'owner-id',
      ownerDisplayName: 'Owner',
      documentType: 'General',
      currentRevisionNumber: 2,
      title: 'Document',
      content: 'Current content',
      status: 'Active',
      tags: [],
      createdAt: '2026-07-18T00:00:00Z',
    };
    const revisions: RevisionSummary[] = [2, 1].map((revisionNumber) => ({
      id: `revision-${revisionNumber}`,
      revisionNumber,
      title: `Revision ${revisionNumber}`,
      createdByUserId: 'owner-id',
      createdByUserName: 'Owner',
      createdAt: `2026-07-1${revisionNumber}T00:00:00Z`,
    }));
    const api = {
      getKnowledgeItem: vi.fn().mockReturnValue(of(item)),
      listRevisions: vi.fn().mockReturnValue(
        of({ items: revisions, page: 1, pageSize: 20, totalCount: 2, totalPages: 1 }),
      ),
      listComments: vi.fn().mockReturnValue(
        of({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }),
      ),
    };

    await TestBed.configureTestingModule({
      imports: [KnowledgeDetailPage],
      providers: [
        provideRouter([]),
        { provide: ApiClient, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { scope: 'Project' },
              paramMap: convertToParamMap({ id: item.id }),
            },
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(KnowledgeDetailPage);
    fixture.detectChanges();

    const layout = fixture.nativeElement.querySelector('.document-layout') as HTMLElement;
    const rail = layout.querySelector('.revision-rail') as HTMLElement;
    const labels = Array.from(rail.querySelectorAll('.revision-item')).map((button) =>
      button.textContent?.trim(),
    );

    expect(layout.firstElementChild?.classList.contains('article')).toBe(true);
    expect(layout.lastElementChild).toBe(rail);
    expect(labels).toEqual(['Latest', '2', '1']);
    expect(
      (fixture.nativeElement.querySelector('.detail-toolbar a') as HTMLAnchorElement).getAttribute(
        'href',
      ),
    ).toBe('/project-documents');
    expect(fixture.nativeElement.querySelector('.detail-toolbar a')?.textContent?.trim()).toBe(
      'Back to project documents',
    );
    expect(fixture.nativeElement.querySelector('.detail-page > .comments-panel')).not.toBeNull();
  });

  it('redirects a legacy project document URL to the project document route', async () => {
    const item: KnowledgeItem = {
      id: 'project-document-id',
      scope: 'Project',
      projectId: 'project-id',
      ownerUserId: 'owner-id',
      ownerDisplayName: 'Owner',
      documentType: 'General',
      currentRevisionNumber: 1,
      title: 'Project document',
      content: 'Content',
      status: 'Active',
      tags: [],
      createdAt: '2026-07-18T00:00:00Z',
    };
    const api = {
      getKnowledgeItem: vi.fn().mockReturnValue(of(item)),
    };

    await TestBed.configureTestingModule({
      imports: [KnowledgeDetailPage],
      providers: [
        provideRouter([]),
        { provide: ApiClient, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { scope: 'Personal' },
              paramMap: convertToParamMap({ id: item.id }),
            },
          },
        },
      ],
    }).compileComponents();

    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    TestBed.createComponent(KnowledgeDetailPage).detectChanges();

    expect(navigate).toHaveBeenCalledWith(
      ['/project-documents/detail', item.id],
      { replaceUrl: true },
    );
  });
});
