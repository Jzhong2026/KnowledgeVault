# KnowledgeVault Project Map

Snapshot updated on 2026-05-13. Always refresh with `rg --files` before relying on this for active work.

## Roots

- Repository: `D:\AI\Projects\KnowledgeVault`
- Frontend: `D:\AI\Projects\KnowledgeVault\src\knowledge-vault-web`
- Backend solution: `D:\AI\Projects\KnowledgeVault\src\KnowledgeVault`
- Backend API project: `D:\AI\Projects\KnowledgeVault\src\KnowledgeVault\KnowledgeVault`

## Backend Projects

```text
KnowledgeVault                 # ASP.NET Core API, controllers, middleware, auth wiring
KnowledgeVault.Domain          # Entities and enums
KnowledgeVault.Contracts       # DTOs, provider interfaces, current-user contract
KnowledgeVault.DataAccess      # EF Core DbContext, SQLite config, migrations
KnowledgeVault.Infrastructure  # Reusable JWT, password hashing, time, exceptions, text helpers
KnowledgeVault.Providers       # Business logic for auth, categories, tags, knowledge items, lookups
```

## Backend Notes

- Target framework: `net10.0`.
- Database: SQLite via EF Core migrations.
- Local EF tool manifest: `src\KnowledgeVault\dotnet-tools.json`.
- Startup runs migrations in development or when `Database:AutoMigrate` is `true`.
- API logs use Serilog compact JSON lines in the app content root `logs` directory.
- Controllers should stay thin and delegate to provider interfaces.
- Authentication uses JWT bearer tokens.
- Knowledge base basics currently include users, categories, tags, knowledge items, and `KnowledgeItemStatus`.

Useful backend commands from `D:\AI\Projects\KnowledgeVault\src\KnowledgeVault`:

```powershell
dotnet build .\KnowledgeVault.slnx
dotnet run --project .\KnowledgeVault\KnowledgeVault.csproj
dotnet tool run dotnet-ef migrations add <Name> --project .\KnowledgeVault.DataAccess\KnowledgeVault.DataAccess.csproj --startup-project .\KnowledgeVault\KnowledgeVault.csproj --output-dir Migrations
```

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
