import { expect, test } from '@playwright/test';
import { authenticate, loginViaApi } from './auth';

/**
 * e2e verification of the implemented planning document
 * (docs/plans/2026-07-17-project-documents-revisions-mcp-plan.zh-CN.md):
 *
 *   - The app boots behind an auth guard and exposes the planned English UI
 *     (Project Documents / My Workspace navigation, Categories, Tags).
 *   - Opening a document (the core "plan result") shows the document body,
 *     a Revision history rail and a Comments panel, and switching a revision
 *     updates the displayed revision.
 *
 * Run locally (backend on :5030, Angular dev server on :4200):
 *   npm run e2e:install      # first time only: download Chromium
 *   npm run e2e              # starts ng serve if needed and runs the checks
 */

test.describe('KnowledgeVault plan result', () => {
  test('authenticated app shows the planned English navigation', async ({ page, request }) => {
    const auth = await loginViaApi(request);
    await authenticate(page, auth);

    await page.goto('/dashboard');

    const sidebar = page.locator('aside.sidebar');
    await expect(sidebar).toBeVisible();

    // English UI chrome is a hard acceptance criterion of the plan.
    await expect(sidebar.getByText('Projects')).toBeVisible();
    await expect(sidebar.getByText('My Workspace')).toBeVisible();
    await expect(sidebar.getByText('Documents').first()).toBeVisible();
    await expect(sidebar.getByText('Categories')).toBeVisible();
    await expect(sidebar.getByText('Tags')).toBeVisible();
  });

  test('opening a document shows revision history and comments', async ({ page, request }) => {
    const auth = await loginViaApi(request);
    await authenticate(page, auth);

    // Pick a real document through the same API the UI uses.
    const docsResponse = await request.get('/KnowledgeVault/api/documents', {
      params: { page: 1, pageSize: 1 },
    });
    expect(docsResponse.ok()).toBeTruthy();

    const docsBody = (await docsResponse.json()) as {
      items?: Array<{ id: string; title?: string }>;
    };
    const documentId = docsBody.items?.[0]?.id;
    expect(documentId, 'No documents available to open — seed at least one document.').toBeTruthy();

    // /project-documents/detail redirects to the correct scope route automatically.
    await page.goto(`/project-documents/detail/${documentId}`);

    // Document body renders.
    const detail = page.locator('section.detail-page');
    await expect(detail).toBeVisible();
    await expect(detail.locator('h2')).toBeVisible();

    // Markdown/article body is present.
    await expect(page.locator('.markdown-body')).toBeVisible();

    // Revision history rail.
    const revisionRail = page.locator('aside[aria-label="Document revisions"]');
    await expect(revisionRail).toBeVisible();
    await expect(revisionRail.getByRole('heading', { name: 'Revisions', level: 3 })).toBeVisible();
    await expect(revisionRail.locator('button.revision-item').first()).toBeVisible();

    // Comments panel.
    const commentsPanel = page.locator('section.comments-panel');
    await expect(commentsPanel).toBeVisible();
    await expect(commentsPanel.getByRole('heading', { name: 'Comments', level: 3 })).toBeVisible();
    await expect(
      commentsPanel.locator('textarea[placeholder^="Add a comment to revision"]'),
    ).toBeVisible();

    // Switching to a non-latest revision updates the displayed revision badge.
    const revisionButtons = revisionRail.locator('button.revision-item:not(.revision-item--latest)');
    const extraRevisions = await revisionButtons.count();
    if (extraRevisions > 0) {
      await revisionButtons.first().click();
      await expect(page.locator('.badge--accent', { hasText: /Revision \d+/ })).toBeVisible();
    }
  });
});
