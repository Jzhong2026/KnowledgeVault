---
name: knowledge-vault
description: Project context for the user's KnowledgeVault personal knowledge base application. Use when Codex works on the KnowledgeVault frontend or backend, sees paths under D:\AI\Projects\KnowledgeVault\src\knowledge-vault-web or D:\AI\Projects\KnowledgeVault\src\KnowledgeVault, or the user mentions this personal knowledge base, future RAG integration, Angular frontend, ASP.NET Core backend, API integration, document ingestion, search, embeddings, or knowledge vault features.
---

# Knowledge Vault

## Core Context

Treat KnowledgeVault as a personal knowledge base product. The current plan includes future RAG features, so preserve clean domain boundaries around documents, metadata, search, embeddings, retrieval, and chat/answering flows as the app grows.

The frontend and backend are sibling projects:

- Frontend: `D:\AI\Projects\KnowledgeVault\src\knowledge-vault-web`
- Backend: `D:\AI\Projects\KnowledgeVault\src\KnowledgeVault`

When starting work, verify the current shape with `rg --files` in both roots. The initial scaffold snapshot is in `references/project-map.md`; read it when path/layout context matters.

## Frontend

Use the Angular project in `knowledge-vault-web`.

- Framework: Angular 21 with SSR files present.
- Package manager: npm.
- Useful commands from the frontend root:
  - `npm start`
  - `npm run build`
  - `npm test`
- Key files at scaffold time: `src/app/app.ts`, `src/app/app.routes.ts`, `src/app/app.html`, `src/app/app.css`, `src/styles.css`, `angular.json`, `package.json`.

Follow existing Angular conventions before adding new structure. Keep UI work oriented around the actual personal knowledge base experience, not a marketing landing page.

## Backend

Use the ASP.NET Core project under `KnowledgeVault\KnowledgeVault`.

- Framework: ASP.NET Core on `net10.0`.
- API project should keep controllers thin; place business logic in provider classes.
- Current backend is split into API, Domain, Contracts, DataAccess, Infrastructure, and Providers projects.
- SQLite is accessed through EF Core migrations in the DataAccess project.
- API logs use Serilog compact JSON lines under the app content root `logs` directory.
- Useful command from `D:\AI\Projects\KnowledgeVault\src\KnowledgeVault`:
  - `dotnet run --project .\KnowledgeVault\KnowledgeVault.csproj`
  - `dotnet build .\KnowledgeVault.slnx`
  - `dotnet tool run dotnet-ef migrations add <Name> --project .\KnowledgeVault.DataAccess\KnowledgeVault.DataAccess.csproj --startup-project .\KnowledgeVault\KnowledgeVault.csproj --output-dir Migrations`
- Development URLs from launch settings:
  - HTTP: `http://localhost:5073`
  - HTTPS: `https://localhost:7091`

Keep API changes compatible with the Angular app. Prefer clear service/domain boundaries as RAG-related features arrive.

## Product Direction

Remember these durable assumptions unless the user changes them:

- The product is a personal knowledge vault.
- RAG is planned but not necessarily implemented yet.
- Likely future capabilities include document import, chunking, embeddings, semantic search, citations, and conversational answers over the user's own content.
- Avoid hard-coding provider choices for embeddings/vector storage until the user selects the stack.

## Working Habits

Before changing code, inspect both sides when the task crosses the frontend/backend boundary. For API work, update or check the backend contract and frontend integration together. For RAG-related work, favor incremental, testable pieces: storage model, ingestion, chunking, embedding interface, retrieval API, then UI.
