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
