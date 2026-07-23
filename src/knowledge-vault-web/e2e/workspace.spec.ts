import { expect, test } from '@playwright/test';
import { authenticate, loginViaApi } from './auth';

/**
 * e2e coverage for the Workspace / Folder plan:
 *   docs/plan_project_documents_workspace.md
 *
 * The backend folder API already exists (FoldersController at
 * /KnowledgeVault/api/folders) and is exercised by the `folder REST API`
 * suite below — those checks run today against a locally-started backend.
 *
 * The frontend Workspace UI (FolderTile / TileGrid / FolderTree / WorkspaceMode,
 * Open/Exit Workspace, deep-link restore) is NOT yet implemented in this
 * workspace, so the `UI acceptance` suite is authored against the plan's
 * intended component/selector names and marked `test.fixme`. Flip each
 * `test.fixme` to `test` once the corresponding component ships.
 *
 * Run locally (backend on :5030, Angular dev server on :4200):
 *   npm run e2e:install      # first time only
 *   npm run e2e              # starts ng serve if needed + runs the checks
 *
 * Credentials: KV_TEST_USER / KV_TEST_PASSWORD (Personal scope is used so no
 * project membership is required).
 */

const FOLDERS = '/KnowledgeVault/api/folders';
const DOCUMENTS = '/KnowledgeVault/api/documents';
const SCOPE = 'Personal';

function uniqName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
}

// ---------------------------------------------------------------------------
// Data-layer coverage — runs now (backend is implemented).
// ---------------------------------------------------------------------------
test.describe('Workspace plan — folder REST API', () => {
  let headers: Record<string, string>;
  const folderIds: string[] = [];
  const docIds: string[] = [];

  test.beforeAll(async ({ request }) => {
    const auth = await loginViaApi(request);
    headers = { Authorization: `Bearer ${auth.token}` };
  });

  test.afterAll(async ({ request }) => {
    // Delete deepest-first so parents are empty before removal.
    for (const id of [...folderIds].reverse()) {
      await request.delete(`${FOLDERS}/${id}`, { headers });
    }
    for (const id of docIds) {
      await request.delete(`${DOCUMENTS}/${id}`, { headers });
    }
  });

  test('creates multi-level nested folders', async ({ request }) => {
    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: uniqName('ws-root') },
    });
    expect(root.ok(), 'root folder create failed').toBeTruthy();
    const rootBody = (await root.json()) as { id: string };
    folderIds.push(rootBody.id);

    const child = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: uniqName('ws-child') },
    });
    expect(child.ok(), 'child folder create failed').toBeTruthy();
    const childBody = (await child.json()) as { id: string; parentFolderId: string | null };
    folderIds.push(childBody.id);
    expect(childBody.parentFolderId).toBe(rootBody.id);

    const grand = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: childBody.id, name: uniqName('ws-grand') },
    });
    expect(grand.ok(), 'grandchild folder create failed').toBeTruthy();
    const grandBody = (await grand.json()) as { id: string; parentFolderId: string | null };
    folderIds.push(grandBody.id);
    expect(grandBody.parentFolderId).toBe(childBody.id);
  });

  test('lists content and builds a tree rooted at a folder', async ({ request }) => {
    const root = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: uniqName('ws-tree-root') },
    });
    const rootBody = (await root.json()) as { id: string };
    folderIds.push(rootBody.id);

    const child = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: rootBody.id, name: uniqName('ws-tree-child') },
    });
    const childBody = (await child.json()) as { id: string };
    folderIds.push(childBody.id);

    const content = await request.get(FOLDERS, {
      headers,
      params: { scope: SCOPE, parentFolderId: rootBody.id, rootFolderId: rootBody.id },
    });
    expect(content.ok()).toBeTruthy();
    const contentBody = (await content.json()) as {
      folders: Array<{ id: string }>;
      documents: Array<unknown>;
    };
    expect(contentBody.folders.some((f) => f.id === childBody.id)).toBeTruthy();

    const tree = await request.get(`${FOLDERS}/tree`, {
      headers,
      params: { scope: SCOPE, rootFolderId: rootBody.id },
    });
    expect(tree.ok()).toBeTruthy();
    const treeBody = (await tree.json()) as {
      id: string;
      children: Array<{ id: string }>;
    };
    expect(treeBody.id).toBe(rootBody.id);
    expect(treeBody.children.some((c) => c.id === childBody.id)).toBeTruthy();
  });

  test('enforces the workspace root boundary (parentFolderId outside root -> 400)', async ({
    request,
  }) => {
    const a = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: uniqName('ws-root-a') },
    });
    const aBody = (await a.json()) as { id: string };
    folderIds.push(aBody.id);

    const b = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: uniqName('ws-root-b') },
    });
    const bBody = (await b.json()) as { id: string };
    folderIds.push(bBody.id);

    // b is NOT a descendant of a -> must be rejected as outside the workspace root.
    const resp = await request.get(FOLDERS, {
      headers,
      params: { scope: SCOPE, parentFolderId: bBody.id, rootFolderId: aBody.id },
    });
    expect(resp.status()).toBe(400);
  });

  test('moves a document into a folder (Move to folder)', async ({ request }) => {
    const list = await request.get(DOCUMENTS, {
      headers,
      params: { scope: SCOPE, page: 1, pageSize: 1 },
    });
    const listBody = (await list.json()) as { items?: Array<{ id: string }> };
    const docId = listBody.items?.[0]?.id;
    test.skip(!docId, 'No Personal document available to move — seed one first.');

    const folder = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: uniqName('ws-move-target') },
    });
    const folderBody = (await folder.json()) as { id: string };
    folderIds.push(folderBody.id);

    try {
      const moved = await request.patch(`${DOCUMENTS}/${docId}/folder`, {
        headers,
        data: { folderId: folderBody.id },
      });
      expect(moved.ok(), 'document move failed').toBeTruthy();

      const content = await request.get(FOLDERS, {
        headers,
        params: { scope: SCOPE, parentFolderId: folderBody.id, rootFolderId: folderBody.id },
      });
      const contentBody = (await content.json()) as { documents: Array<{ id: string }> };
      expect(contentBody.documents.some((d) => d.id === docId)).toBeTruthy();
    } finally {
      // Restore the document to the root so we leave no data behind.
      await request.patch(`${DOCUMENTS}/${docId}/folder`, {
        headers,
        data: { folderId: null },
      });
    }
  });

  test('rejects deleting a non-empty folder with 409', async ({ request }) => {
    const parent = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, name: uniqName('ws-nonempty') },
    });
    const parentBody = (await parent.json()) as { id: string };
    folderIds.push(parentBody.id);

    const child = await request.post(FOLDERS, {
      headers,
      data: { scope: SCOPE, parentFolderId: parentBody.id, name: uniqName('ws-nonempty-child') },
    });
    const childBody = (await child.json()) as { id: string };
    folderIds.push(childBody.id);

    const del = await request.delete(`${FOLDERS}/${parentBody.id}`, { headers });
    expect(del.status()).toBe(409);
  });
});

// ---------------------------------------------------------------------------
// UI acceptance — pending frontend implementation (see plan §5/§6/§10).
// Flip test.fixme -> test once the corresponding component is built.
// Selectors follow the plan's component names: app-folder-tile, app-tile-grid,
// app-folder-tree, app-workspace-mode.
// ---------------------------------------------------------------------------
test.describe('Workspace plan — UI acceptance (pending frontend)', () => {
  test.fixme('Folder and Document tiles render in the grid', async ({ page, request }) => {
    const auth = await loginViaApi(request);
    await authenticate(page, auth);
    await page.goto('/knowledge');
    await expect(page.locator('app-tile-grid').first()).toBeVisible();
    await expect(page.locator('app-folder-tile').first()).toBeVisible();
    await expect(page.locator('app-document-tile').first()).toBeVisible();
  });

  test.fixme('left-clicking a Folder tile opens it and enters Workspace mode', async ({
    page,
    request,
  }) => {
    const auth = await loginViaApi(request);
    await authenticate(page, auth);
    await page.goto('/knowledge');
    await page.locator('app-folder-tile').first().click();
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();
    await expect(page.locator('app-folder-tree').first()).toBeVisible();
    await expect(page.locator('aside.sidebar')).toBeHidden();
  });

  test.fixme('Open Workspace is reachable from tile, toolbar and tree node', async ({
    page,
    request,
  }) => {
    const auth = await loginViaApi(request);
    await authenticate(page, auth);
    await page.goto('/knowledge');
    await expect(
      page.locator('app-folder-tile').first().getByRole('button', { name: /Open Workspace/i }),
    ).toBeVisible();
    await expect(
      page.getByRole('button', { name: /Open Workspace/i }).first(),
    ).toBeVisible();
  });

  test.fixme('Exit Workspace restores the normal sidebar navigation', async ({ page, request }) => {
    const auth = await loginViaApi(request);
    await authenticate(page, auth);
    await page.goto('/knowledge');
    await page.locator('app-folder-tile').first().click();
    await page.getByRole('button', { name: /Exit Workspace/i }).click();
    await expect(page.locator('aside.sidebar')).toBeVisible();
    await expect(page.locator('app-workspace-mode').first()).toBeHidden();
  });

  test.fixme('deep link restores Workspace mode from query params', async ({ page, request }) => {
    const auth = await loginViaApi(request);
    await authenticate(page, auth);
    const root = await request.post(FOLDERS, {
      headers: { Authorization: `Bearer ${auth.token}` },
      data: { scope: SCOPE, name: uniqName('ws-deeplink') },
    });
    const rootBody = (await root.json()) as { id: string };
    await page.goto(`/knowledge?workspaceRootFolderId=${rootBody.id}&folderId=${rootBody.id}`);
    await expect(page.locator('app-workspace-mode').first()).toBeVisible();
  });
});
