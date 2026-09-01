import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { AuthService } from '../../../core/auth/auth.service';
import { WorkspaceService } from '../../../core/workspace/workspace.service';
import { KnowledgeItem, KnowledgeItemSummary } from '../../../core/models/knowledge.models';
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

describe('WorkspacePage document id copy (workspace mode)', () => {
  const documentId = '92e7d576-6a81-4541-95a1-98983907a411';
  const rootFolderId = 'root-folder-id';

  const activeDocument: KnowledgeItem = {
    id: documentId,
    scope: 'Personal',
    ownerUserId: 'owner-id',
    ownerDisplayName: 'Owner',
    documentType: 'General',
    currentRevisionNumber: 1,
    title: 'RW-73202-wave1-wave2-review-triage.md',
    content: '# Triage',
    status: 'Active',
    tags: [],
    createdAt: '2026-09-01T00:00:00Z',
  };

  const emptyPage = {
    items: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
  };

  function buildApi() {
    return {
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
      getFolderTree: vi.fn().mockReturnValue(
        of({ id: rootFolderId, name: 'Implementation', children: [] }),
      ),
      getKnowledgeItem: vi.fn().mockReturnValue(of(activeDocument)),
      listRevisions: vi.fn().mockReturnValue(of(emptyPage)),
      listDocumentComments: vi.fn().mockReturnValue(of(emptyPage)),
      listDocumentOwners: vi.fn().mockReturnValue(of([])),
      listCategories: vi.fn().mockReturnValue(of([])),
      listTags: vi.fn().mockReturnValue(of([])),
      listProjects: vi
        .fn()
        .mockReturnValue(of({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
    };
  }

  async function createWorkspaceFixture(writeText?: ReturnType<typeof vi.fn>) {
    const api = buildApi();
    const queryParamMap = new BehaviorSubject(
      convertToParamMap({
        workspaceRootFolderId: rootFolderId,
        folderId: rootFolderId,
      }),
    );

    await TestBed.configureTestingModule({
      imports: [WorkspacePage],
      providers: [
        provideRouter([]),
        { provide: ApiClient, useValue: api },
        { provide: AuthService, useValue: { currentUser: () => null } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { data: { scope: 'Personal' } },
            queryParamMap: queryParamMap.asObservable(),
          },
        },
      ],
    }).compileComponents();

    if (writeText) {
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText },
      });
    }

    const fixture = TestBed.createComponent(WorkspacePage);
    fixture.detectChanges();
    return { fixture, api };
  }

  it('copies the active document id from the header id strip', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    const { fixture } = await createWorkspaceFixture(writeText);
    const workspace = TestBed.inject(WorkspaceService);

    workspace.openDocumentTab(documentId, activeDocument.title);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const strip = fixture.nativeElement.querySelector(
      '[data-testid="active-document-id"]',
    ) as HTMLButtonElement;

    expect(strip).not.toBeNull();
    expect(strip.querySelector('.workspace-doc__id-value')?.textContent?.trim()).toBe(documentId);

    strip.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(writeText).toHaveBeenCalledWith(documentId);
    expect(fixture.componentInstance.copyMessage()).toBe('Document id copied');
  });

  it('copies the document id from a tab icon without switching tabs', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    const { fixture } = await createWorkspaceFixture(writeText);
    const workspace = TestBed.inject(WorkspaceService);

    workspace.openDocumentTab(documentId, activeDocument.title);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const tabIcon = fixture.nativeElement.querySelector(
      '[data-testid="tab-copy-document-id"]',
    ) as HTMLElement;

    expect(tabIcon).not.toBeNull();
    expect(tabIcon.getAttribute('aria-label')).toBe('Copy document id');

    const activeTabIdBefore = workspace.activeTabIdSignal();
    tabIcon.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(writeText).toHaveBeenCalledWith(documentId);
    expect(workspace.activeTabIdSignal()).toBe(activeTabIdBefore);
    expect(fixture.componentInstance.copyMessage()).toBe('Document id copied');
  });

  it('reports an error when the clipboard is unavailable', async () => {
    const writeText = vi.fn().mockRejectedValue(new Error('denied'));
    const { fixture } = await createWorkspaceFixture(writeText);
    const workspace = TestBed.inject(WorkspaceService);

    // Force the legacy execCommand path to fail as well so the error branch runs.
    const execCommand = vi.fn().mockReturnValue(false);
    Object.defineProperty(document, 'execCommand', {
      configurable: true,
      value: execCommand,
    });

    workspace.openDocumentTab(documentId, activeDocument.title);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const strip = fixture.nativeElement.querySelector(
      '[data-testid="active-document-id"]',
    ) as HTMLButtonElement;

    await fixture.componentInstance.copyDocumentId(new Event('click'), documentId);

    expect(strip).not.toBeNull();
    expect(fixture.componentInstance.copyMessage()).toBeNull();
    expect(fixture.componentInstance.error()).toBe(
      'Unable to access the clipboard. Please copy the id manually.',
    );
  });
});
