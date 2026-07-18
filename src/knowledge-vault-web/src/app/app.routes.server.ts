import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
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
    path: '**',
    renderMode: RenderMode.Prerender
  }
];
