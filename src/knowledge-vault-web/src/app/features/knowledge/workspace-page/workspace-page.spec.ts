import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { AuthService } from '../../../core/auth/auth.service';
import { KnowledgeItemSummary } from '../../../core/models/knowledge.models';
import { WorkspacePage } from './workspace-page';

describe('WorkspacePage document navigation', () => {
  it('preserves the selected project folder when opening a project document', async () => {
    const queryParamMap = new BehaviorSubject(
      convertToParamMap({ projectId: 'project-id', browseFolderId: 'folder-id' }),
    );
    const api = {
      listFolderContent: vi.fn().mockReturnValue(
        of({
          folders: [],
          documents: [],
          page: 1,
          pageSize: 20,
          totalFolderCount: 0,
          totalDocumentCount: 0,
          hasMoreFolders: false,
          hasMoreDocuments: false,
          hasMore: false,
        }),
      ),
      getFolder: vi.fn().mockReturnValue(
        of({
          id: 'folder-id',
          name: 'Voice',
          parentFolderId: null,
          projectId: 'project-id',
          scope: 'Project',
          sortOrder: 0,
          childFolderCount: 0,
          documentCount: 1,
          creatorDisplayName: 'Owner',
          isArchived: false,
        }),
      ),
      listDocumentOwners: vi.fn().mockReturnValue(of([])),
      listCategories: vi.fn().mockReturnValue(of([])),
      listTags: vi.fn().mockReturnValue(of([])),
      listProjects: vi
        .fn()
        .mockReturnValue(of({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
    };

    await TestBed.configureTestingModule({
      imports: [WorkspacePage],
      providers: [
        provideRouter([]),
        { provide: ApiClient, useValue: api },
        { provide: AuthService, useValue: { currentUser: () => null } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { data: { scope: 'Project' } },
            queryParamMap: queryParamMap.asObservable(),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(WorkspacePage);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const document: KnowledgeItemSummary = {
      id: 'document-id',
      scope: 'Project',
      projectId: 'project-id',
      projectName: 'Atlas',
      ownerUserId: 'owner-id',
      ownerDisplayName: 'Owner',
      documentType: 'General',
      currentRevisionNumber: 1,
      title: 'Voice guide',
      status: 'Active',
      tags: [],
      createdAt: '2026-09-01T00:00:00Z',
    };

    fixture.componentInstance.openDocument(document);

    expect(navigate).toHaveBeenCalledWith(['/project-documents/detail', document.id], {
      replaceUrl: true,
      queryParams: { projectId: 'project-id', browseFolderId: 'folder-id' },
    });
  });
});
