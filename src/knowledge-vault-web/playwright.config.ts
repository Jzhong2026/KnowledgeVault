import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright config for KnowledgeVault web e2e checks.
 *
 * Targets the locally-started Angular dev server (npm start -> ng serve, port 4200).
 * The ASP.NET Core backend must already be running and reachable on port 5030
 * (the dev proxy in proxy.conf.json forwards /KnowledgeVault -> http://localhost:5030).
 *
 * Login credentials are read from the environment (see e2e/auth.ts):
 *   KV_TEST_USER     account userName or email
 *   KV_TEST_PASSWORD account password
 * Provide them via a .env file (loaded automatically) or exported shell variables.
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: /(.+\.)?(spec|test)\.[cm]?[jt]s/,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  timeout: 60_000,
  expect: { timeout: 15_000 },

  // Start (or reuse) the Angular dev server. The backend on :5030 must be up separately.
  webServer: {
    command: 'npm start',
    url: 'http://localhost:4200',
    reuseExistingServer: true,
    timeout: 240_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },

  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
