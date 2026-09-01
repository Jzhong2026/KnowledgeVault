import { expect, test, type Route } from '@playwright/test';

/**
 * e2e coverage for copying a document id (GUID) out of workspace mode.
 *
 * The workspace view renders documents in in-page tabs, so the id never
 * appears in the URL. Two affordances expose it:
 *   1. a copy-id icon inside each tab (revealed on hover / keyboard focus)
 *   2. an always-visible monospace id strip under the active document title
 *
 * Both write the raw GUID to the clipboard and surface a "Document id copied"
 * status message, which is what a user would paste into an MCP request.
 *
 * All API calls are mocked via page.route so the suite is hermetic.
 */

const DOCUMENT_ID = '92e7d576-6a81-4541-95a1-98983907a411';
const OTHER_DOCUMENT_ID = 'b41f0a92-3d77-4e18-9c60-2fa7c1de55b3';
const ROOT_FOLDER_ID = 'root-folder-id';

const documents: Record<string, unknown> = {
  [DOCUMENT_ID]: {
    id: DOCUMENT_ID,
    scope: 'Personal',
    ownerUserId: 'owner-id',
    ownerDisplayName: 'Owner',
    documentType: 'General',
    currentRevisionNumber: 1,
    title: 'RW-73202-wave1-wave2-review-triage.md',
    content: '# Triage\n\nWave 1 findings.',
    status: 'Active',
    tags: [],
    createdAt: '2026-09-01T00:00:00Z',
  },
  [OTHER_DOCUMENT_ID]: {
    id: OTHER_DOCUMENT_ID,
    scope: 'Personal',
    ownerUserId: 'owner-id',
    ownerDisplayName: 'Owner',
    documentType: 'General',
    currentRevisionNumber: 1,
    title: 'wave1-summary.md',
    content: '# Summary',
    status: 'Active',
    tags: [],
    createdAt: '2026-09-01T00:00:00Z',
  },
};

const emptyPage = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };

/** Documents listed in the workspace sidebar tree, in the shape the
 *  WorkspacePage exposes to FolderTree (KnowledgeItemSummary). */
const treeDocuments = Object.values(documents).map((doc) => {
  const item = doc as { id: string; title: string };
  return {
    id: item.id,
    scope: 'Personal',
    ownerUserId: 'owner-id',
    ownerDisplayName: 'Owner',
    documentType: 'General',
    currentRevisionNumber: 1,
    title: item.title,
    status: 'Active',
    tags: [],
    createdAt: '2026-09-01T00:00:00Z',
  };
});

async function fulfillJson(route: Route, body: unknown): Promise<void> {
  await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
}

async function stubApi(page: import('@playwright/test').Page): Promise<void> {
  await page.route('**/KnowledgeVault/api/**', async (route) => {
    const url = route.request().url();

    if (url.includes('/folders/tree')) {
      await fulfillJson(route, { id: ROOT_FOLDER_ID, name: 'Implementation', children: [] });
      return;
    }
    if (url.includes('/folders')) {
      await fulfillJson(route, {
        folders: [],
        documents: treeDocuments,
        page: 1,
        pageSize: 20,
        totalFolderCount: 0,
        totalDocumentCount: treeDocuments.length,
        hasMoreFolders: false,
        hasMoreDocuments: false,
        hasMore: false,
      });
      return;
    }
    if (url.includes('/documents/') && url.includes('/revisions')) {
      await fulfillJson(route, emptyPage);
      return;
    }
    if (url.includes('/comments')) {
      await fulfillJson(route, emptyPage);
      return;
    }
    const documentMatch = url.match(/\/documents\/([^/?]+)(?:\?|$)/);
    if (documentMatch) {
      const fixture = documents[documentMatch[1]];
      if (fixture) {
        await fulfillJson(route, fixture);
        return;
      }
    }
    await fulfillJson(route, []);
  });
}

/** Click a document in the workspace sidebar tree to open it as a tab. */
async function openDocumentFromTree(
  page: import('@playwright/test').Page,
  titleFragment: string,
): Promise<void> {
  const doc = page.locator('.folder-tree__doc', { hasText: titleFragment }).first();
  await expect(doc).toBeVisible();
  await doc.click();
}

async function authenticate(page: import('@playwright/test').Page): Promise<void> {
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
}

test.describe('Workspace mode — copy document id', () => {
  test.beforeEach(async ({ page, context }) => {
    // Chromium needs clipboard permissions to read back what was written.
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await authenticate(page);
    await stubApi(page);
  });

  test('shows the document id in a strip and copies it on click', async ({ page }) => {
    const pageErrors: string[] = [];
    page.on('pageerror', (error) => pageErrors.push(error.message));
    page.on('console', (message) => {
      if (message.type() === 'error') pageErrors.push(message.text());
    });

    await page.goto(`/knowledge?workspaceRootFolderId=${ROOT_FOLDER_ID}&folderId=${ROOT_FOLDER_ID}`);
    await expect(page.locator('.workspace-shell__tabs')).toBeVisible();

    // In workspace mode documents are opened from the sidebar folder tree.
    await openDocumentFromTree(page, 'RW-73202');

    const strip = page.locator('[data-testid="active-document-id"]');
    await expect(strip).toBeVisible();
    await expect(strip.locator('.workspace-doc__id-value')).toHaveText(DOCUMENT_ID);

    await strip.click();
    await expect(page.locator('.workspace-doc__copy-message')).toHaveText('Document id copied');

    const clipboard = await page.evaluate(() => navigator.clipboard.readText());
    expect(clipboard).toBe(DOCUMENT_ID);

    await expect.poll(() => pageErrors).toEqual([]);
  });

  test('tab copy-id icon is hidden until hover and copies without switching tabs', async ({ page }) => {
    await page.goto(`/knowledge?workspaceRootFolderId=${ROOT_FOLDER_ID}&folderId=${ROOT_FOLDER_ID}`);
    await expect(page.locator('.workspace-shell__tabs')).toBeVisible();

    // First tab.
    await openDocumentFromTree(page, 'RW-73202');
    await expect(page.locator('[data-testid="active-document-id"]')).toHaveText(
      new RegExp(DOCUMENT_ID),
    );

    // Second tab, now active.
    await openDocumentFromTree(page, 'wave1-summary');
    await expect(page.locator('[data-testid="active-document-id"]')).toHaveText(
      new RegExp(OTHER_DOCUMENT_ID),
    );

    const firstTab = page.locator('.workspace-tab', { hasText: 'RW-73202' }).first();
    const copyIcon = firstTab.locator('[data-testid="tab-copy-document-id"]');

    // Hidden by default, visible on hover.
    await expect(copyIcon).toHaveCSS('opacity', '0');
    await firstTab.hover();
    await expect(copyIcon).toHaveCSS('opacity', '1');

    await copyIcon.click();

    // The click must not activate the tab it belongs to.
    await expect(page.locator('[data-testid="active-document-id"]')).toHaveText(
      new RegExp(OTHER_DOCUMENT_ID),
    );
    await expect(page.locator('.workspace-doc__copy-message')).toHaveText('Document id copied');

    const clipboard = await page.evaluate(() => navigator.clipboard.readText());
    expect(clipboard).toBe(DOCUMENT_ID);
  });

  test('copy-id icon is reachable by keyboard', async ({ page }) => {
    await page.goto(`/knowledge?workspaceRootFolderId=${ROOT_FOLDER_ID}&folderId=${ROOT_FOLDER_ID}`);
    await expect(page.locator('.workspace-shell__tabs')).toBeVisible();

    await openDocumentFromTree(page, 'RW-73202');
    await expect(page.locator('[data-testid="active-document-id"]')).toHaveText(
      new RegExp(DOCUMENT_ID),
    );

    const copyIcon = page
      .locator('.workspace-tab', { hasText: 'RW-73202' })
      .first()
      .locator('[data-testid="tab-copy-document-id"]');

    await copyIcon.focus();
    await expect(copyIcon).toBeFocused();
    await page.keyboard.press('Enter');

    await expect(page.locator('.workspace-doc__copy-message')).toHaveText('Document id copied');
    const clipboard = await page.evaluate(() => navigator.clipboard.readText());
    expect(clipboard).toBe(DOCUMENT_ID);
  });
});
