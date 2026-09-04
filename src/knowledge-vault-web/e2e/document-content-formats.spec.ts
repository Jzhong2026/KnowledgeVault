import { expect, test } from '@playwright/test';

const documents = [
  { id: 'markdown-document', title: 'notes.md', content: '# Markdown heading' },
  { id: 'text-document', title: 'notes.txt', content: '# Literal heading\n  preserved spacing' },
  { id: 'json-document', title: 'settings.json', content: '{"name":"vault","enabled":true}' },
  {
    id: 'html-document',
    title: 'workflow.html',
    content: `<!doctype html>
      <html>
        <head><style>h1 { color: rgb(1, 2, 3); }</style></head>
        <body>
          <h1 id="section">HTML workflow</h1>
          <a href="#section">Jump locally</a>
          <a href="https://external.example/page">External page</a>
          <img src="https://external.example/pixel.png">
          <script>document.body.dataset.scriptExecuted = 'true';</script>
        </body>
      </html>`,
  },
];

test('renders Markdown, text, JSON, and static HTML according to the document extension', async ({ page }) => {
  const externalRequests: string[] = [];
  page.on('request', (request) => {
    if (request.url().startsWith('https://external.example/')) {
      externalRequests.push(request.url());
    }
  });

  await page.addInitScript(() => {
    localStorage.setItem('knowledge-vault.auth', JSON.stringify({
      token: 'playwright-token',
      expiresAt: '2099-01-01T00:00:00.000Z',
      user: { id: 'playwright-user', userName: 'playwright', email: 'playwright@example.test' },
    }));
  });

  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname.toLowerCase();
    const document = documents.find((item) => path.endsWith(`/documents/${item.id}`));
    if (document) {
      await route.fulfill({ json: {
        ...document,
        scope: 'Personal', ownerUserId: 'playwright-user', ownerDisplayName: 'Playwright',
        documentType: 'General', currentRevisionNumber: 1, status: 'Active', tags: [],
        createdAt: '2026-08-13T00:00:00Z',
      } });
      return;
    }
    if (path.endsWith('/folders')) {
      await route.fulfill({ json: {
        folders: [],
        documents: documents.map((item) => ({
          ...item, content: undefined,
          scope: 'Personal', ownerUserId: 'playwright-user', ownerDisplayName: 'Playwright',
          documentType: 'General', currentRevisionNumber: 1, status: 'Active', tags: [],
          createdAt: '2026-08-13T00:00:00Z',
        })),
        page: 1, pageSize: 20, totalFolderCount: 0, totalDocumentCount: 3,
        hasMoreFolders: false, hasMoreDocuments: false, hasMore: false,
      } });
      return;
    }
    if (path.includes('/revisions') || path.includes('/comments')) {
      await route.fulfill({ json: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } });
      return;
    }
    await route.fulfill({ json: [] });
  });

  await page.goto('/dashboard');
  await page.locator('a[href="/knowledge"]').first().click();

  await page.getByText('notes.md', { exact: true }).click();
  await expect(page.locator('.article-body h1')).toHaveText('Markdown heading');

  await page.locator('a[href="/knowledge"]').first().click();
  await page.getByText('notes.txt', { exact: true }).click();
  const textContent = page.locator('.article-body .plain-content');
  await expect(textContent).toHaveText('# Literal heading\n  preserved spacing');
  await expect(page.locator('.article-body h1')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Full screen' })).toBeVisible();

  await page.locator('a[href="/knowledge"]').first().click();
  await page.getByText('settings.json', { exact: true }).click();
  await expect(page.locator('.article-body .plain-content')).toContainText('"name": "vault"');
  await expect(page.locator('.article-body .plain-content')).toContainText('"enabled": true');

  await page.locator('a[href="/knowledge"]').first().click();
  await page.getByText('workflow.html', { exact: true }).click();
  const htmlFrame = page.frameLocator('.article-body iframe.static-html-preview');
  await expect(htmlFrame.locator('h1')).toHaveText('HTML workflow');
  await expect(htmlFrame.locator('h1')).toHaveCSS('color', 'rgb(1, 2, 3)');
  await expect(htmlFrame.locator('body')).not.toHaveAttribute('data-script-executed', 'true');
  await expect(htmlFrame.locator('a[href="#section"]')).toHaveCount(1);
  await expect(htmlFrame.locator('a[href^="https://external.example"]')).toHaveCount(0);
  await expect(htmlFrame.locator('img[src^="https://external.example"]')).toHaveCount(0);
  expect(externalRequests).toEqual([]);

  await page.getByRole('button', { name: 'Full screen' }).click();
  const fullscreenHtmlFrame = page.frameLocator('.fullscreen-document iframe.static-html-preview');
  await expect(fullscreenHtmlFrame.locator('h1')).toHaveText('HTML workflow');
});
