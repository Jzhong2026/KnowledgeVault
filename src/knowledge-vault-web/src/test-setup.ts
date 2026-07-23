import '@analogjs/vite-plugin-angular/setup-vitest';
import { setupTestBed } from '@analogjs/vitest-angular/setup-testbed';

// Zone-based Angular testing environment. The specs rely on TestBed +
// compileComponents + fixture.whenStable, which require zone.js (patched by
// setup-vitest) and the classic BrowserTestingModule test environment.
setupTestBed({ zoneless: false });
