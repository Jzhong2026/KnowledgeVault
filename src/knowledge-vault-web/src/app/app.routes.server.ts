import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  // Authenticated, dynamic routes (the data depends on the caller's localStorage
  // session, so the server cannot pre-render them). Forcing Client mode here
  // also prevents the catch-all Prerender entry from serving the wrong HTML
  // for /knowledge and /project-documents.
  {
    path: 'knowledge',
    renderMode: RenderMode.Client
  },
  {
    path: 'project-documents',
    renderMode: RenderMode.Client
  },
  {
    path: 'knowledge/detail/:id',
    renderMode: RenderMode.Client
  },
  {
    path: 'project-documents/detail/:id',
    renderMode: RenderMode.Client
  },
  {
    path: 'projects/:id',
    renderMode: RenderMode.Client
  },
  {
    path: 'projects',
    renderMode: RenderMode.Client
  },
  {
    path: 'profile',
    renderMode: RenderMode.Client
  },
  {
    path: 'settings/categories',
    renderMode: RenderMode.Client
  },
  {
    path: 'settings/tags',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Client
  }
];

