import { expect, test, type Route } from '@playwright/test';

/**
 * e2e coverage for the document detail breadcrumb (replaces the legacy
 * "Back to project documents" link with a full folder trail).
 *
 * The Angular `KnowledgeDetailPage` reads `browseFolderId` from the route
 * query params, then walks up the folder chain with one `GET /folders/{id}`
 * call per ancestor. The breadcrumb renders:
 *   Project Documents / <project name> / <ancestor folders...> / <doc title>
 * where every segment except the trailing title is a router link back to
 * the document list at the right folder.
 *
 * To keep this test deterministic and hermetic, all `/KnowledgeVault/api/**`
 * calls are mocked via `page.route`. The fixtures below match the
 * shapes produced by the real backend (KnowledgeItem, FolderDetail,
 * ProjectSummary).
 */

const DOCUMENT_ID = 'doc-id';
const PROJECT_ID = 'project-id';
const GUIDE_FOLDER_ID = 'guides-folder-id';
const VOICE_FOLDER_ID = 'voice-folder-id';

const folders: Record<string, unknown> = {
  [VOICE_FOLDER_ID]: {
    id: VOICE_FOLDER_ID,
    name: 'Voice',
    parentFolderId: GUIDE_FOLDER_ID,
    projectId: PROJECT_ID,
    scope: 'Project',
    sortOrder: 0,
    childFolderCount: 0,
    documentCount: 1,
    creatorDisplayName: 'Owner',
    isArchived: false,
  },
  [GUIDE_FOLDER_ID]: {
    id: GUIDE_FOLDER_ID,
    name: 'Guides',
    parentFolderId: null,
    projectId: PROJECT_ID,
    scope: 'Project',
    sortOrder: 0,
    childFolderCount: 1,
    documentCount: 0,
    creatorDisplayName: 'Owner',
    isArchived: false,
  },
};

const project = {
  id: PROJECT_ID,
  name: 'Atlas',
  ownerUserId: 'owner-id',
  isArchived: false,
  currentUserRole: 'Owner',
  isFollowing: true,
  members: [],
  createdAt: '2026-09-01T00:00:00Z',
};

const document = {
  id: DOCUMENT_ID,
  scope: 'Project',
  projectId: PROJECT_ID,
  ownerUserId: 'owner-id',
  ownerDisplayName: 'Owner',
  documentType: 'General',
  currentRevisionNumber: 1,
  title: 'Voice guide',
  content: '# Voice guide\n\nbody',
  status: 'Active',
  tags: [],
  createdAt: '2026-09-01T00:00:00Z',
};

async function fulfillJson(route: Route, body: unknown): Promise<void> {
  await route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

test.describe('Document detail breadcrumb', () => {
  test('renders a project + folder trail that links back to the selected folder', async ({ page }) => {
    const pageErrors: string[] = [];
    const folderRequests: string[] = [];
    page.on('pageerror', (error) => pageErrors.push(error.message));
    page.on('console', (message) => {
      if (message.type() === 'error') pageErrors.push(message.text());
    });

    await page.addInitScript(() => {
      localStorage.setItem(
        'knowledge-vault.auth',
        JSON.stringify({
          token: 'playwright-token',
          expiresAt: '2099-01-01T00:00:00.000Z',
          user: { id: 'playwright-user', userName: 'playwright', email: 'playwright@example.test' },
        }),
      );
    });

    await page.route('**/KnowledgeVault/api/**', async (route) => {
      const url = route.request().url();
      if (url.includes(`/documents/${DOCUMENT_ID}`) && !url.includes('/revisions') && !url.includes('/comments')) {
        await fulfillJson(route, document);
        return;
      }
      if (url.includes(`/projects/${PROJECT_ID}`) && !url.includes('/topics')) {
        await fulfillJson(route, project);
        return;
      }
      const folderMatch = url.match(/\/folders\/([^/?]+)(?:\?|$)/);
      if (folderMatch) {
        folderRequests.push(folderMatch[1]);
        const fixture = folders[folderMatch[1]];
        if (fixture) {
          await fulfillJson(route, fixture);
          return;
        }
        await route.fulfill({ status: 404, contentType: 'application/json', body: '{"error":"not found"}' });
        return;
      }
      if (url.includes('/documents/') && url.includes('/revisions')) {
        await fulfillJson(route, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
        return;
      }
      if (url.includes('/comments')) {
        await fulfillJson(route, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
        return;
      }
      await fulfillJson(route, []);
    });

    await page.goto(
      `/project-documents/detail/${DOCUMENT_ID}?projectId=${PROJECT_ID}&browseFolderId=${VOICE_FOLDER_ID}`,
    );

    const breadcrumb = page.locator('[data-testid="document-breadcrumb"]');
    await expect(breadcrumb).toBeVisible();
    await expect(breadcrumb).toHaveAttribute('aria-label', 'Document location');

    const labels = await breadcrumb.evaluate((node) =>
      Array.from(node.querySelectorAll('a, span')).map((el) => el.textContent?.trim() ?? ''),
    );
    expect(labels).toEqual([
      'Project Documents',
      '/',
      'Atlas',
      '/',
      'Guides',
      '/',
      'Voice',
      '/',
      'Voice guide',
    ]);

    const current = breadcrumb.locator('.document-breadcrumb__current');
    await expect(current).toHaveText('Voice guide');
    await expect(current).toHaveAttribute('title', 'Voice guide');

    const voiceLink = breadcrumb.locator('a', { hasText: 'Voice' });
    await expect(voiceLink).toHaveAttribute(
      'href',
      `/project-documents?projectId=${PROJECT_ID}&browseFolderId=${VOICE_FOLDER_ID}`,
    );

    const guidesLink = breadcrumb.locator('a', { hasText: 'Guides' });
    await expect(guidesLink).toHaveAttribute(
      'href',
      `/project-documents?projectId=${PROJECT_ID}&browseFolderId=${GUIDE_FOLDER_ID}`,
    );

    const projectLink = breadcrumb.locator('a', { hasText: 'Atlas' });
    await expect(projectLink).toHaveAttribute('href', `/project-documents?projectId=${PROJECT_ID}`);

    const rootLink = breadcrumb.locator('a', { hasText: 'Project Documents' });
    await expect(rootLink).toHaveAttribute('href', '/project-documents');

    // The walker should request the leaf folder first, then its parent. We
    // don't require a specific order beyond "both folders were fetched".
    expect(folderRequests.sort()).toEqual([GUIDE_FOLDER_ID, VOICE_FOLDER_ID].sort());

    await expect.poll(() => pageErrors).toEqual([]);
  });

  test('shows only the project root when no browseFolderId is present', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem(
        'knowledge-vault.auth',
        JSON.stringify({
          token: 'playwright-token',
          expiresAt: '2099-01-01T00:00:00.000Z',
          user: { id: 'playwright-user', userName: 'playwright', email: 'playwright@example.test' },
        }),
      );
    });

    const folderRequests: string[] = [];
    await page.route('**/KnowledgeVault/api/**', async (route) => {
      const url = route.request().url();
      if (url.includes(`/documents/${DOCUMENT_ID}`) && !url.includes('/revisions') && !url.includes('/comments')) {
        await fulfillJson(route, document);
        return;
      }
      if (url.includes(`/projects/${PROJECT_ID}`) && !url.includes('/topics')) {
        await fulfillJson(route, project);
        return;
      }
      const folderMatch = url.match(/\/folders\/([^/?]+)(?:\?|$)/);
      if (folderMatch) {
        folderRequests.push(folderMatch[1]);
        const fixture = folders[folderMatch[1]];
        if (fixture) {
          await fulfillJson(route, fixture);
          return;
        }
        await route.fulfill({ status: 404, contentType: 'application/json', body: '{"error":"not found"}' });
        return;
      }
      if (url.includes('/documents/') && url.includes('/revisions')) {
        await fulfillJson(route, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
        return;
      }
      if (url.includes('/comments')) {
        await fulfillJson(route, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
        return;
      }
      await fulfillJson(route, []);
    });

    await page.goto(`/project-documents/detail/${DOCUMENT_ID}?projectId=${PROJECT_ID}`);

    const breadcrumb = page.locator('[data-testid="document-breadcrumb"]');
    await expect(breadcrumb).toBeVisible();
    await expect(breadcrumb.locator('a')).toHaveCount(2);
    await expect(breadcrumb.locator('a', { hasText: 'Project Documents' })).toHaveAttribute(
      'href',
      '/project-documents',
    );
    await expect(breadcrumb.locator('a', { hasText: 'Atlas' })).toHaveAttribute(
      'href',
      `/project-documents?projectId=${PROJECT_ID}`,
    );
    await expect(breadcrumb.locator('.document-breadcrumb__current')).toHaveText('Voice guide');
    expect(folderRequests).toEqual([]);
  });

  test('Personal documents keep the legacy "Back to my documents" link', async ({ page }) => {
    const personalDocument = {
      ...document,
      id: 'personal-doc-id',
      scope: 'Personal',
      projectId: null,
    };

    await page.addInitScript(() => {
      localStorage.setItem(
        'knowledge-vault.auth',
        JSON.stringify({
          token: 'playwright-token',
          expiresAt: '2099-01-01T00:00:00.000Z',
          user: { id: 'playwright-user', userName: 'playwright', email: 'playwright@example.test' },
        }),
      );
    });

    await page.route('**/KnowledgeVault/api/**', async (route) => {
      const url = route.request().url();
      if (url.includes('/documents/personal-doc-id') && !url.includes('/revisions') && !url.includes('/comments')) {
        await fulfillJson(route, personalDocument);
        return;
      }
      if (url.includes('/documents/') && url.includes('/revisions')) {
        await fulfillJson(route, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
        return;
      }
      if (url.includes('/comments')) {
        await fulfillJson(route, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
        return;
      }
      await fulfillJson(route, []);
    });

    await page.goto('/knowledge/detail/personal-doc-id');

    const toolbar = page.locator('.detail-toolbar');
    await expect(toolbar.locator('[data-testid="document-breadcrumb"]')).toHaveCount(0);
    const backLink = toolbar.locator('a', { hasText: 'Back to my documents' });
    await expect(backLink).toBeVisible();
    await expect(backLink).toHaveAttribute('href', '/knowledge');
  });
});
