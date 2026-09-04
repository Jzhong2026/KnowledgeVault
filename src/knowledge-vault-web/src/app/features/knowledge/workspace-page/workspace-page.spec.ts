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

describe('WorkspacePage file import', () => {
  const designCategory = {
    id: 'design-id',
    name: 'Design',
    sortOrder: 0,
    isArchived: false,
    isSystem: true,
    createdAt: '2026-09-01T00:00:00Z',
  };

  function folderPage(documents: KnowledgeItemSummary[] = []) {
    return {
      folders: [],
      documents,
      page: 1,
      pageSize: 20,
      totalFolderCount: 0,
      totalDocumentCount: documents.length,
      hasMoreFolders: false,
      hasMoreDocuments: false,
      hasMore: false,
    };
  }

  function summary(id: string, title: string, revision = 1): KnowledgeItemSummary {
    return {
      id,
      scope: 'Personal',
      ownerUserId: 'owner-id',
      ownerDisplayName: 'Owner',
      documentType: 'General',
      currentRevisionNumber: revision,
      title,
      status: 'Active',
      tags: [],
      createdAt: '2026-09-01T00:00:00Z',
    };
  }

  function item(doc: KnowledgeItemSummary, content: string): KnowledgeItem {
    return { ...doc, content };
  }

  function dropEvent(file: File): DragEvent {
    return {
      preventDefault() {},
      stopPropagation() {},
      dataTransfer: {
        types: ['Files'],
        files: [file],
        items: [],
        dropEffect: 'copy',
      },
    } as unknown as DragEvent;
  }

  async function createImportFixture(options: {
    documents?: KnowledgeItemSummary[];
    existingContent?: Record<string, KnowledgeItem>;
  } = {}) {
    const documents = options.documents ?? [];
    const api = {
      listFolderContent: vi.fn().mockReturnValue(of(folderPage(documents))),
      listDocumentOwners: vi.fn().mockReturnValue(of([])),
      listCategories: vi.fn().mockReturnValue(of([designCategory])),
      listTags: vi.fn().mockReturnValue(of([])),
      listProjects: vi
        .fn()
        .mockReturnValue(of({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
      getKnowledgeItem: vi.fn().mockImplementation((id: string) => {
        const existing = options.existingContent?.[id];
        return of(existing ?? item(summary(id, 'unknown'), ''));
      }),
      createKnowledgeItem: vi.fn().mockImplementation((request: { title: string; content: string }) =>
        of(item(summary('created-id', request.title), request.content)),
      ),
      updateKnowledgeItem: vi.fn().mockImplementation((id: string, request: { title: string; content: string }) =>
        of(item(summary(id, request.title, 2), request.content)),
      ),
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
            snapshot: { data: { scope: 'Personal' } },
            queryParamMap: of(convertToParamMap({})),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(WorkspacePage);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, api };
  }

  async function dropTextFile(
    fixture: { componentInstance: WorkspacePage; nativeElement: HTMLElement },
    name: string,
    content: string,
  ): Promise<void> {
    await fixture.componentInstance.onFileDrop(dropEvent(new File([content], name, { type: 'text/plain' })));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  function conflictDialog(nativeElement: HTMLElement): HTMLElement | null {
    return nativeElement.querySelector('.import-conflict-dialog');
  }

  it('creates a new document using the original filename', async () => {
    const { fixture, api } = await createImportFixture();

    await dropTextFile(fixture, 'test.txt', 'hello');

    expect(conflictDialog(fixture.nativeElement)).toBeNull();
    expect(api.createKnowledgeItem).toHaveBeenCalledWith(expect.objectContaining({
      title: 'test.txt',
      content: 'hello',
    }));
    expect(api.updateKnowledgeItem).not.toHaveBeenCalled();
  });

  it('renames an existing stem-named document to the dropped filename', async () => {
    const existing = summary('doc-test', 'test');
    const { fixture, api } = await createImportFixture({
      documents: [existing],
      existingContent: { [existing.id]: item(existing, 'hello') },
    });

    await dropTextFile(fixture, 'test.txt', 'hello');

    expect(conflictDialog(fixture.nativeElement)).toBeNull();
    expect(api.createKnowledgeItem).not.toHaveBeenCalled();
    expect(api.updateKnowledgeItem).toHaveBeenCalledWith(existing.id, expect.objectContaining({
      title: 'test.txt',
      content: 'hello',
      expectedRevisionNumber: 1,
    }));
  });

  it('skips an update when the dropped filename and content already match', async () => {
    const existing = summary('doc-test', 'test.txt');
    const { fixture, api } = await createImportFixture({
      documents: [existing],
      existingContent: { [existing.id]: item(existing, 'hello') },
    });

    await dropTextFile(fixture, 'test.txt', 'hello');

    expect(conflictDialog(fixture.nativeElement)).toBeNull();
    expect(api.createKnowledgeItem).not.toHaveBeenCalled();
    expect(api.updateKnowledgeItem).not.toHaveBeenCalled();
  });

  it('creates a revision when dropped content differs from the matched document', async () => {
    const existing = summary('doc-test', 'test.txt');
    const { fixture, api } = await createImportFixture({
      documents: [existing],
      existingContent: { [existing.id]: item(existing, 'hello') },
    });

    await dropTextFile(fixture, 'test.txt', 'hello world');

    expect(conflictDialog(fixture.nativeElement)).toBeNull();
    expect(api.createKnowledgeItem).not.toHaveBeenCalled();
    expect(api.updateKnowledgeItem).toHaveBeenCalledWith(existing.id, expect.objectContaining({
      title: 'test.txt',
      content: 'hello world',
      expectedRevisionNumber: 1,
    }));
  });

  it('does not create another revision when the same file is dropped again', async () => {
    const created = summary('created-id', 'test.txt');
    let documents: KnowledgeItemSummary[] = [];
    const existingContent: Record<string, KnowledgeItem> = {};
    const api = {
      listFolderContent: vi.fn().mockImplementation(() => of(folderPage(documents))),
      listDocumentOwners: vi.fn().mockReturnValue(of([])),
      listCategories: vi.fn().mockReturnValue(of([designCategory])),
      listTags: vi.fn().mockReturnValue(of([])),
      listProjects: vi
        .fn()
        .mockReturnValue(of({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
      getKnowledgeItem: vi.fn().mockImplementation((id: string) => of(existingContent[id] ?? item(summary(id, 'unknown'), ''))),
      createKnowledgeItem: vi.fn().mockImplementation((request: { title: string; content: string }) => {
        documents = [created];
        existingContent[created.id] = item(created, request.content);
        return of(existingContent[created.id]);
      }),
      updateKnowledgeItem: vi.fn(),
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
            snapshot: { data: { scope: 'Personal' } },
            queryParamMap: of(convertToParamMap({})),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(WorkspacePage);
    fixture.detectChanges();
    await fixture.whenStable();

    await dropTextFile(fixture, 'test.txt', 'hello');
    await dropTextFile(fixture, 'test.txt', 'hello');

    expect(conflictDialog(fixture.nativeElement)).toBeNull();
    expect(api.createKnowledgeItem).toHaveBeenCalledTimes(1);
    expect(api.updateKnowledgeItem).not.toHaveBeenCalled();
  });

  it('creates a new file when several stem-related documents exist without an exact name match', async () => {
    const stem = summary('doc-test', 'test');
    const markdown = summary('doc-md', 'test.md');
    const { fixture, api } = await createImportFixture({
      documents: [stem, markdown],
      existingContent: {
        [stem.id]: item(stem, 'hello'),
        [markdown.id]: item(markdown, 'hello'),
      },
    });

    await dropTextFile(fixture, 'test.txt', 'hello');

    expect(conflictDialog(fixture.nativeElement)).toBeNull();
    expect(api.updateKnowledgeItem).not.toHaveBeenCalled();
    expect(api.createKnowledgeItem).toHaveBeenCalledWith(expect.objectContaining({
      title: 'test.txt',
      content: 'hello',
    }));
  });
});
