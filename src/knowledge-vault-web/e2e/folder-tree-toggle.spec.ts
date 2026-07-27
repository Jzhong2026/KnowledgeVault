import { expect, test } from '@playwright/test';

const root = {
  id: 'root',
  name: 'Root',
  sortOrder: 0,
  children: [
    {
      id: 'parent',
      name: 'Parent folder',
      parentFolderId: 'root',
      sortOrder: 0,
      children: [
        { id: 'nested-leaf', name: 'Nested leaf', parentFolderId: 'parent', sortOrder: 0, children: [] },
      ],
    },
    { id: 'sibling-leaf', name: 'Sibling leaf', parentFolderId: 'root', sortOrder: 1, children: [] },
  ],
};

test('every workspace folder, including unselected nested and sibling folders, shows an expand icon', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('knowledge-vault.auth', JSON.stringify({
      token: 'playwright-token',
      expiresAt: '2099-01-01T00:00:00.000Z',
      user: { id: 'playwright-user', userName: 'playwright', email: 'playwright@example.test' },
    }));
  });

  await page.route('**/KnowledgeVault/api/**', async (route) => {
    const url = route.request().url();
    if (url.includes('/folders/tree')) {
      await route.fulfill({ json: root });
      return;
    }
    if (url.includes('/folders')) {
      await route.fulfill({
        json: {
          folders: [], documents: [], page: 1, pageSize: 100,
          totalFolderCount: 0, totalDocumentCount: 0,
          hasMoreFolders: false, hasMoreDocuments: false, hasMore: false,
        },
      });
      return;
    }
    await route.fulfill({ json: [] });
  });

  await page.goto('/knowledge?workspaceRootFolderId=root&folderId=root');

  const rows = page.locator('app-folder-tree .folder-tree__row');
  await expect(rows).toHaveCount(4);
  for (let index = 0; index < 4; index += 1) {
    const icon = rows.nth(index).locator('.folder-tree__toggle svg');
    await expect(icon).toBeVisible();
    await expect(icon).toHaveJSProperty('clientWidth', 14);
    await expect(icon).toHaveJSProperty('clientHeight', 14);
  }
});
