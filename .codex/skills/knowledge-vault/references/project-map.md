# KnowledgeVault Project Map

Snapshot captured on 2026-05-13 from the initial scaffolds. Always refresh with `rg --files` before relying on this for active work.

## Roots

- Frontend: `D:\AI\Projects\KnowledgeVault\src\knowledge-vault-web`
- Backend solution: `D:\AI\Projects\KnowledgeVault\src\KnowledgeVault`
- Backend project: `D:\AI\Projects\KnowledgeVault\src\KnowledgeVault\KnowledgeVault`

## Frontend Files

```text
angular.json
package.json
package-lock.json
README.md
tsconfig.json
tsconfig.app.json
tsconfig.spec.json
public\favicon.ico
src\index.html
src\main.ts
src\main.server.ts
src\server.ts
src\styles.css
src\app\app.config.ts
src\app\app.config.server.ts
src\app\app.css
src\app\app.html
src\app\app.routes.ts
src\app\app.routes.server.ts
src\app\app.spec.ts
src\app\app.ts
```

## Frontend Package Notes

- Angular packages are currently `^21.2.x`.
- SSR is present via `@angular/ssr`, `src/server.ts`, and server route/config files.
- Scripts:
  - `npm start`: Angular dev server.
  - `npm run build`: production build.
  - `npm test`: test runner.
  - `npm run watch`: development build watch.

## Backend Files

```text
KnowledgeVault.slnx
KnowledgeVault\KnowledgeVault.csproj
KnowledgeVault\Program.cs
KnowledgeVault\WeatherForecast.cs
KnowledgeVault\KnowledgeVault.http
KnowledgeVault\appsettings.json
KnowledgeVault\appsettings.Development.json
KnowledgeVault\Properties\launchSettings.json
KnowledgeVault\Controllers\WeatherForecastController.cs
```

## Backend Project Notes

- Target framework: `net10.0`.
- Nullable and implicit usings are enabled.
- Package reference: `Microsoft.AspNetCore.OpenApi` `10.0.8`.
- `Program.cs` currently registers controllers, OpenAPI, HTTPS redirection, authorization, and maps controllers.
- Development OpenAPI is mapped only in development.
- Launch profiles:
  - `http`: `http://localhost:5073`
  - `https`: `https://localhost:7091;http://localhost:5073`
