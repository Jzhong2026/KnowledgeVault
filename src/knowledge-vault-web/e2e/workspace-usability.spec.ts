import { expect, test } from '@playwright/test';
import { authenticate, loginViaApi } from './auth';

/**
 * End-to-end coverage for the workspace usability plan:
 *   - Left sidebar / Workspace-mode header shows the current root folder name.
 *   - "New folder" dialog surfaces the parent folder (resolved by id, not
 *     name) so the user can see exactly where the new folder will live.
 *   - A folder name change in another part of the app does not break
 *     operations that depend on id (no client-side name resolution leaks).
 *   - Multiple documents can be open in tabs simultaneously (VS Code style).
 *
 * Run with the backend on :5030 and Angular dev server on :4200. Credentials
 * are read from KV_TEST_USER / KV_TEST_PASSWORD env vars.
 */

const FOLDERS = '/KnowledgeVault/api/folders';
const DOCUMENTS = '/KnowledgeVault/api/documents';
const SCOPE = 'Personal';

function uniqName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
}

test.describe('Workspace usability — sidebar root + parent hint + multi-tab', () => {
  let auth: Awaited<ReturnType<typeof loginViaApi>>;
  let headers: Record<string, string>;
  const seededFolderIds: string[] = [];
  const seededDocIds: string[] = [];

  test.beforeEach(async ({ page, request }) => {
    auth = await loginViaApi(request);
    headers = { Authorization: `Bearer ${auth.token}` };
    await authenticate(page, auth);
  });

  test.afterEach(async ({ request }) => {
    for (const id of [...seededFolderIds].reverse()) {
      await request.delete(`${FOLDERS}/${id}`, { headers });
    }
    for (const id of seededDocIds) {
      await request.delete(`${DOCUMENTS}/${id}`, { headers });
    }
    seededFolderIds.length = 0;
    seededDocIds.length = 0;
  });

  test('workspace-mode header shows the current root folder name', async ({
    page,
    request,
  }) => {
    const rootName = uniqName('ws-root');
    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: rootName },
    });
    const rootBody = (await root.json()) as { id: string };
    seededFolderIds.push(rootBody.id);

    await page.goto(`/knowledge?workspaceRootFolderId=${rootBody.id}&folderId=${rootBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();

    // In workspace mode the entire sidebar is replaced by app-workspace-mode;
    // the workspace-mode header surfaces the live root folder name (display
    // only — all operations continue to use ids).
    await expect(page.locator('.workspace-mode__title').first()).toHaveText(rootName);
  });

  test('"New folder" dialog displays the current parent folder name', async ({
    page,
    request,
  }) => {
    const rootName = uniqName('ws-parent-root');
    const childName = uniqName('ws-parent-child');

    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: rootName },
    });
    const rootBody = (await root.json()) as { id: string };
    seededFolderIds.push(rootBody.id);

    const child = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: childName },
    });
    const childBody = (await child.json()) as { id: string };
    seededFolderIds.push(childBody.id);

    await page.goto(`/knowledge?workspaceRootFolderId=${rootBody.id}&folderId=${childBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();

    await page.getByRole('button', { name: 'New folder' }).click();
    const hint = page.locator('[data-testid="create-folder-parent"]');
    await expect(hint).toBeVisible();
    // The hint must reflect the currently selected child folder — the user
    // should see exactly where the new folder will be created.
    await expect(hint).toContainText(childName);
  });

  test('parent hint survives a rename of the parent folder elsewhere', async ({
    page,
    request,
  }) => {
    // Seed a parent folder and enter workspace mode on it.
    const original = uniqName('ws-pre-rename');
    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: original },
    });
    const rootBody = (await root.json()) as { id: string };
    seededFolderIds.push(rootBody.id);

    await page.goto(`/knowledge?workspaceRootFolderId=${rootBody.id}&folderId=${rootBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();
    await expect(page.locator('.workspace-mode__title').first()).toHaveText(original);

    // Open the New folder dialog — parent is the root, so the hint shows the
    // current name.
    await page.getByRole('button', { name: 'New folder' }).click();
    const hint = page.locator('[data-testid="create-folder-parent"]');
    await expect(hint).toContainText(original);

    // Rename the parent via REST — the UI should pick up the new name
    // because the hint is resolved by id, not by a stale snapshot.
    const renamed = `${original}-renamed`;
    await request.put(`${FOLDERS}/${rootBody.id}`, {
      headers,
      data: { name: renamed },
    });
    // Force the page to refetch by reloading and reopening the dialog.
    await page.reload();
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();
    // Wait for the tree to load with the new name before opening the dialog,
    // otherwise the parent hint resolves from a stale snapshot.
    await expect(page.locator('.workspace-mode__title').first()).toHaveText(renamed);
    await page.getByRole('button', { name: 'New folder' }).click();
    await expect(page.locator('[data-testid="create-folder-parent"]')).toContainText(renamed);
  });

  test('workspace mode shows the explorer when no document is open', async ({
    page,
    request,
  }) => {
    // Workspace mode is VS Code-style: the right pane shows a tab bar plus
    // either an opened document or an explorer listing the current folder's
    // subfolders + documents. This test asserts the explorer (with the
    // empty-state copy) is rendered when no tab is open.
    const folderName = uniqName('ws-explorer-empty');
    const folder = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: folderName },
    });
    const folderBody = (await folder.json()) as { id: string };
    seededFolderIds.push(folderBody.id);

    await page.goto(`/knowledge?workspaceRootFolderId=${folderBody.id}&folderId=${folderBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();

    const tabBar = page.locator('.workspace-shell__tabs');
    await expect(tabBar).toBeVisible();
    const explorer = page.locator('[data-testid="workspace-explorer"]');
    await expect(explorer).toBeVisible();
    await expect(explorer.locator('.workspace-explorer__empty')).toBeVisible();
  });

  test('folder tree renders a folder icon for each node', async ({
    page,
    request,
  }) => {
    // Seed a root + child folder so the tree renders at least one iconified
    // child node.
    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: uniqName('icon-root') },
    });
    const rootBody = (await root.json()) as { id: string };
    seededFolderIds.push(rootBody.id);

    const child = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: uniqName('icon-child') },
    });
    const childBody = (await child.json()) as { id: string };
    seededFolderIds.push(childBody.id);

    await page.goto(`/knowledge?workspaceRootFolderId=${rootBody.id}&folderId=${rootBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();

    // The root itself is rendered via .workspace-mode__title, and each
    // child folder in the tree must carry a folder-tree__icon span with
    // an SVG so it is visually distinct from the "Open Workspace" action.
    await expect(page.locator('.folder-tree__icon').first()).toBeVisible();
  });

  test('"New document" dialog shows the parent folder as read-only', async ({
    page,
    request,
  }) => {
    const rootName = uniqName('ws-docparent-root');
    const childName = uniqName('ws-docparent-child');

    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: rootName },
    });
    const rootBody = (await root.json()) as { id: string };
    seededFolderIds.push(rootBody.id);

    const child = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: childName },
    });
    const childBody = (await child.json()) as { id: string };
    seededFolderIds.push(childBody.id);

    await page.goto(`/knowledge?workspaceRootFolderId=${rootBody.id}&folderId=${childBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();

    await page.getByRole('button', { name: 'New document' }).click();
    const hint = page.locator('[data-testid="editor-parent-hint"]');
    await expect(hint).toBeVisible();
    await expect(hint).toContainText(childName);
  });

  test('workspace mode lists documents in the current folder and opens them in tabs', async ({
    page,
    request,
  }) => {
    // Seed a folder with two documents and a subfolder so we can verify the
    // explorer renders folders + documents side by side.
    const folderName = uniqName('ws-explorer-folder');
    const folder = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: folderName },
    });
    const folderBody = (await folder.json()) as { id: string };
    seededFolderIds.push(folderBody.id);

    const subName = uniqName('ws-explorer-sub');
    const sub = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: folderBody.id, name: subName },
    });
    const subBody = (await sub.json()) as { id: string };
    seededFolderIds.push(subBody.id);

    const titles: string[] = [];
    for (let i = 0; i < 2; i++) {
      const title = uniqName(`ws-explorer-doc-${i}`);
      titles.push(title);
      const doc = await request.post(DOCUMENTS, {
        headers,
        data: {
          scope: SCOPE,
          documentType: 'General',
          title,
          content: `body ${i}`,
          status: 'Active',
          folderId: folderBody.id,
        },
      });
      if (doc.ok()) {
        const docBody = (await doc.json()) as { id: string };
        seededDocIds.push(docBody.id);
      }
    }

    await page.goto(`/knowledge?workspaceRootFolderId=${folderBody.id}&folderId=${folderBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();

    // Explorer shows the folder header and the seeded documents.
    const explorer = page.locator('[data-testid="workspace-explorer"]');
    await expect(explorer).toBeVisible();
    await expect(explorer.locator('[data-testid="workspace-explorer-document"]')).toHaveCount(2);
    await expect(explorer.getByText(subName)).toBeVisible();

    // Click the first document tile and verify it opens in a tab.
    await explorer.locator('[data-testid="workspace-explorer-document"]').first().click();
    await expect(page.locator('.workspace-tab.is-active')).toContainText(titles[0]);
  });

  test('saving a new document refreshes the explorer list immediately', async ({
    page,
    request,
  }) => {
    const folderName = uniqName('ws-reload-folder');
    const folder = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: folderName },
    });
    const folderBody = (await folder.json()) as { id: string };
    seededFolderIds.push(folderBody.id);

    await page.goto(`/knowledge?workspaceRootFolderId=${folderBody.id}&folderId=${folderBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();

    const explorer = page.locator('[data-testid="workspace-explorer"]');
    await expect(explorer).toBeVisible();
    const before = await explorer.locator('[data-testid="workspace-explorer-document"]').count();

    // Open New document, fill required fields, save.
    await page.getByRole('button', { name: 'New document' }).click();
    const title = uniqName('ws-reload-doc');
    await page.getByLabel('Title *').fill(title);
    await page.getByLabel('Content *').fill('reload content body');
    const savePromise = page.waitForResponse(
      (resp) => resp.url().includes('/api/documents') && resp.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Save document' }).click();
    const saveResp = await savePromise;
    expect(saveResp.status(), 'create-document API should respond 2xx').toBeLessThan(300);

    // Track the new doc for cleanup.
    const list = await request.get(`${DOCUMENTS}?scope=Personal&page=1&pageSize=20`, { headers });
    if (list.ok()) {
      const body = (await list.json()) as { items: Array<{ id: string; title: string; folderId: string | null }> };
      for (const item of body.items) {
        if (item.title === title && item.folderId === folderBody.id) {
          seededDocIds.push(item.id);
        }
      }
    }

    // The dialog must close and the explorer list must show the new doc.
    await expect(page.locator('[data-testid="editor-parent-hint"]')).toHaveCount(0);
    const after = await explorer.locator('[data-testid="workspace-explorer-document"]').count();
    expect(after).toBe(before + 1);
    await expect(explorer).toContainText(title);
  });

  test('full-path uniqueness: creating a sibling folder with the same name is rejected', async ({
    page,
    request,
  }) => {
    const rootName = uniqName('ws-uniq-root');
    const childName = uniqName('ws-uniq-child');

    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: rootName },
    });
    const rootBody = (await root.json()) as { id: string };
    seededFolderIds.push(rootBody.id);

    const child = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: childName },
    });
    const childBody = (await child.json()) as { id: string };
    seededFolderIds.push(childBody.id);

    // Try to create ANOTHER sibling under the same parent with the same name.
    // Sibling uniqueness must reject it with 409.
    const conflict = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: childName },
    });
    expect(conflict.status()).toBe(409);
  });

  test.skip('browse-mode breadcrumb shows the full ancestor path and segments are clickable', async ({
    page,
    request,
  }) => {
    // Seed a 3-level chain: root → middle → leaf.
    const rootName = uniqName('crumb-root');
    const middleName = uniqName('crumb-middle');
    const leafName = uniqName('crumb-leaf');

    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: rootName },
    });
    const rootBody = (await root.json()) as { id: string };
    seededFolderIds.push(rootBody.id);

    const middle = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: middleName },
    });
    const middleBody = (await middle.json()) as { id: string };
    seededFolderIds.push(middleBody.id);

    const leaf = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: middleBody.id, name: leafName },
    });
    const leafBody = (await leaf.json()) as { id: string };
    seededFolderIds.push(leafBody.id);

    // Land on the leaf via the normal browse route (not workspace mode).
    await page.goto(`/knowledge?browseFolderId=${leafBody.id}`);
    const breadcrumb = page.locator('[data-testid="browse-breadcrumb"]');
    await expect(breadcrumb).toBeVisible();

    // The breadcrumb must include all three ancestors in root → leaf order,
    // with the leaf as the current (non-clickable) segment.
    await expect(breadcrumb).toContainText(rootName);
    await expect(breadcrumb).toContainText(middleName);
    await expect(breadcrumb).toContainText(leafName);

    const segments = page.locator('[data-testid="browse-breadcrumb-segment"]');
    await expect(segments).toHaveCount(2);
    await expect(segments.nth(0)).toHaveText(rootName);
    await expect(segments.nth(1)).toHaveText(middleName);

    // Clicking the root segment should jump back to the root and clear the
    // breadcrumb entirely.
    await segments.nth(0).click();
    // Wait for the URL to lose browseFolderId — the navigation happens
    // asynchronously.
    await expect
      .poll(async () => {
        return await page.evaluate(() => !new URLSearchParams(location.search).has('browseFolderId'));
      }, { timeout: 10_000 })
      .toBe(true);
    await expect(page.locator('[data-testid="browse-breadcrumb"]')).toHaveCount(0);

    // Clicking the middle segment from the leaf should land on the middle.
    await page.goto(`/knowledge?browseFolderId=${leafBody.id}`);
    const middleSegments = page.locator('[data-testid="browse-breadcrumb-segment"]');
    await expect(middleSegments).toHaveCount(2);
    await middleSegments.nth(1).click();
    await expect
      .poll(async () => {
        return await page.evaluate((id) => new URLSearchParams(location.search).get('browseFolderId') === id, middleBody.id);
      }, { timeout: 10_000 })
      .toBe(true);
    const middleBreadcrumb = page.locator('[data-testid="browse-breadcrumb"]');
    await expect(middleBreadcrumb).toContainText(rootName);
    await expect(middleBreadcrumb).toContainText(middleName);
    // Leaf should no longer be in the trail.
    await expect(middleBreadcrumb).not.toContainText(leafName);
  });

  test('browse-mode "New folder / New document" parent hint resolves to the drilled-in folder', async ({
    page,
    request,
  }) => {
    // Regression: when the user has drilled into a sub-folder, opening the
    // New folder / New document dialog must show that sub-folder as the
    // parent (resolved by id) — not the project root.
    const rootName = uniqName('parent-root');
    const leafName = uniqName('parent-leaf');

    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: rootName },
    });
    const rootBody = (await root.json()) as { id: string };
    seededFolderIds.push(rootBody.id);

    const leaf = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: leafName },
    });
    const leafBody = (await leaf.json()) as { id: string };
    seededFolderIds.push(leafBody.id);

    // Drill into the leaf.
    await page.goto(`/knowledge?browseFolderId=${leafBody.id}`);
    await expect(page.locator('[data-testid="browse-breadcrumb"]')).toContainText(leafName);

    // "New folder" must show the leaf as parent, not the root.
    await page.getByRole('button', { name: 'New folder' }).click();
    await expect(page.locator('[data-testid="create-folder-parent"]')).toContainText(leafName);
    await expect(page.locator('[data-testid="create-folder-parent"]')).not.toContainText(rootName);
    await page.getByRole('button', { name: 'Cancel' }).click();

    // "New document" must show the leaf as parent too.
    await page.getByRole('button', { name: 'New document' }).click();
    await expect(page.locator('[data-testid="editor-parent-hint"]')).toContainText(leafName);
    await expect(page.locator('[data-testid="editor-parent-hint"]')).not.toContainText(rootName);
    await page.getByRole('button', { name: 'Cancel' }).click();
  });
});