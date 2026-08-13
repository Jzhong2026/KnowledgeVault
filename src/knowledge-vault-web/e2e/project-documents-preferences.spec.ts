import { expect, test } from '@playwright/test';

const preferenceKey = 'knowledge-vault.project-documents.playwright-user';

test('Project Documents restores the signed-in user\'s last project and folder', async ({ page }) => {
  await page.addInitScript(({ key }) => {
    localStorage.setItem('knowledge-vault.auth', JSON.stringify({
      token: 'playwright-token',
      expiresAt: '2099-01-01T00:00:00.000Z',
      user: { id: 'playwright-user', userName: 'playwright', email: 'playwright@example.test' },
    }));
    localStorage.setItem(key, JSON.stringify({
      defaultProjectId: 'project-default',
      lastProjectId: 'project-last',
      lastBrowseFolderId: 'folder-last',
    }));
  }, { key: preferenceKey });

  await page.route('**/api/**', async (route) => {
    const url = route.request().url().toLowerCase();
    if (url.includes('/projects')) {
      await route.fulfill({ json: {
        items: [
          { id: 'project-last', name: 'Last project', isFollowing: true },
          { id: 'project-default', name: 'Default project', isFollowing: true },
        ], page: 1, pageSize: 100, totalCount: 2, totalPages: 1,
      } });
      return;
    }
    if (url.includes('/folders')) {
      await route.fulfill({ json: {
        folders: [], documents: [], page: 1, pageSize: 20,
        totalFolderCount: 0, totalDocumentCount: 0,
        hasMoreFolders: false, hasMoreDocuments: false, hasMore: false,
      } });
      return;
    }
    await route.fulfill({ json: [] });
  });

  // Enter through the app shell so the Documents route is a client-side
  // navigation; direct SSR navigation has no browser-side auth preference.
  await page.goto('/dashboard');
  await page.locator('a[href="/project-documents"]').first().click();
  await expect(page).toHaveURL(/projectId=project-last/);
  await expect(page).toHaveURL(/browseFolderId=folder-last/);
  await expect(page.getByTestId('browse-breadcrumb')).toContainText('Last project');
});

test('Project document root filters names and creators, then hides filters in a folder', async ({ page }) => {
  const folderRequests: string[] = [];
  await page.addInitScript(() => {
    localStorage.setItem('knowledge-vault.auth', JSON.stringify({
      token: 'playwright-token',
      expiresAt: '2099-01-01T00:00:00.000Z',
      user: { id: 'playwright-user', userName: 'playwright', email: 'playwright@example.test' },
    }));
    localStorage.setItem('knowledge-vault.project-documents.playwright-user', JSON.stringify({
      defaultProjectId: 'project-one',
      lastProjectId: 'project-one',
      lastBrowseFolderId: null,
    }));
  });

  await page.route('**/api/**', async (route) => {
    const requestUrl = route.request().url();
    const url = requestUrl.toLowerCase();
    if (url.includes('/documents/owners')) {
      await route.fulfill({ json: [
        { id: 'creator-one', displayName: 'Alice Creator' },
        { id: 'playwright-user', displayName: 'Jason (me)' },
        { id: 'creator-two', displayName: 'Bob Creator' },
      ] });
      return;
    }
    if (url.includes('/projects')) {
      await route.fulfill({ json: {
        items: [{ id: 'project-one', name: 'Project one', isFollowing: true }],
        page: 1, pageSize: 100, totalCount: 1, totalPages: 1,
      } });
      return;
    }
    if (url.includes('/folders')) {
      folderRequests.push(requestUrl);
      await route.fulfill({ json: {
        folders: [{
          id: 'folder-one', name: 'Root folder', sortOrder: 0, projectId: 'project-one',
          scope: 'Project', childFolderCount: 0, documentCount: 0, isArchived: false,
        }],
        documents: [{
          id: 'document-one', scope: 'Project', projectId: 'project-one',
          ownerUserId: 'creator-two', ownerDisplayName: 'Bob Creator',
          documentType: 'General', currentRevisionNumber: 1, title: 'Creator document',
          status: 'Active', tags: [], createdAt: '2026-08-13T00:00:00Z',
        }],
        page: 1, pageSize: 20, totalFolderCount: 0, totalDocumentCount: 1,
        hasMoreFolders: false, hasMoreDocuments: false, hasMore: false,
      } });
      return;
    }
    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard');
  await page.locator('a[href="/project-documents"]').first().click();

  await expect(page.getByLabel('Filter by creator')).toBeVisible();
  await expect(page.getByLabel('Filter by creator').locator('option').nth(1)).toHaveText('Jason (me)');
  await expect(page.getByText('Creator: Bob Creator')).toBeVisible();
  await page.getByLabel('Filter by creator').selectOption('creator-two');
  await expect.poll(() => folderRequests.some((url) => url.includes('ownerUserId=creator-two'))).toBe(true);

  await page.getByLabel('Filter folders and documents by name').fill('Creator');
  await page.getByRole('button', { name: 'Filter' }).click();
  await expect.poll(() => folderRequests.some((url) => new URL(url).searchParams.get('search') === 'Creator')).toBe(true);

  await page.getByRole('button', { name: 'List' }).click();
  await expect(page.locator('.document-list__creator')).toHaveText('Bob Creator');

  await page.getByText('Root folder', { exact: true }).click();
  await expect(page.getByLabel('Filter by creator')).toBeHidden();
  await expect(page.getByLabel('Filter folders and documents by name')).toBeHidden();
  await expect.poll(() => folderRequests.some((url) => {
    const params = new URL(url).searchParams;
    return params.get('parentFolderId') === 'folder-one' && !params.has('search') && !params.has('ownerUserId');
  })).toBe(true);
});
