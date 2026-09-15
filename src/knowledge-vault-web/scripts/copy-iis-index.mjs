import { copyFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const browserDir = join(dirname(fileURLToPath(import.meta.url)), '..', 'dist', 'knowledge-vault-web', 'browser');
const csrIndex = join(browserDir, 'index.csr.html');
const iisIndex = join(browserDir, 'index.html');

if (!existsSync(csrIndex)) {
  throw new Error(`Missing ${csrIndex}. Run ng build first.`);
}

copyFileSync(csrIndex, iisIndex);
console.log(`Copied index.csr.html -> index.html for IIS deep links`);
