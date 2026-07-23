import { APIRequestContext, Page } from '@playwright/test';

const STORAGE_KEY = 'knowledge-vault.auth';

interface AuthState {
  token: string;
  expiresAt: string;
  user: { id: string; userName: string; email: string; nickname?: string | null };
}

/**
 * Logs in through the REST API and returns the auth state that the Angular
 * app expects in localStorage (key `knowledge-vault.auth`).
 *
 * Credentials come from the environment so the test never hard-codes secrets:
 *   KV_TEST_USER     -> userName or email
 *   KV_TEST_PASSWORD -> password
 */
export async function loginViaApi(request: APIRequestContext): Promise<AuthState> {
  const userName = process.env.KV_TEST_USER;
  const password = process.env.KV_TEST_PASSWORD;

  if (!userName || !password) {
    throw new Error(
      'Missing test credentials. Set KV_TEST_USER and KV_TEST_PASSWORD ' +
        '(e.g. in a .env file or exported shell variables) before running e2e tests.',
    );
  }

  const response = await request.post('/KnowledgeVault/api/auth/login', {
    data: { userNameOrEmail: userName, password },
  });

  if (!response.ok()) {
    const body = await response.text().catch(() => '');
    throw new Error(
      `Login failed with HTTP ${response.status()} for user "${userName}". ` +
        `Body: ${body.slice(0, 300)}`,
    );
  }

  const payload = (await response.json()) as {
    accessToken: string;
    expiresAt: string;
    user: AuthState['user'];
  };

  return {
    token: payload.accessToken,
    expiresAt: payload.expiresAt,
    user: payload.user,
  };
}

/**
 * Injects the auth state into localStorage for every subsequent navigation so
 * the Angular auth guard treats the browser as authenticated.
 */
export async function authenticate(page: Page, state: AuthState): Promise<void> {
  await page.addInitScript(
    (args: { key: string; value: string }) => {
      window.localStorage.setItem(args.key, args.value);
    },
    { key: STORAGE_KEY, value: JSON.stringify(state) },
  );
}
