import { Routes } from '@angular/router';

import { authGuard, guestGuard } from './core/auth/auth.guard';
import { AppShell } from './layout/app-shell/app-shell';

export const routes: Routes = [
  {
    path: 'auth',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/auth-page/auth-page').then((m) => m.AuthPage),
  },
  {
    path: '',
    component: AppShell,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard-page/dashboard-page').then((m) => m.DashboardPage),
      },
      {
        path: 'knowledge/detail/:id',
        loadComponent: () =>
          import('./features/knowledge/knowledge-detail-page/knowledge-detail-page').then((m) => m.KnowledgeDetailPage),
      },
      {
        path: 'knowledge',
        data: { scope: 'Personal' },
        loadComponent: () => import('./features/knowledge/knowledge-page/knowledge-page').then((m) => m.KnowledgePage),
      },
      {
        path: 'project-documents',
        data: { scope: 'Project' },
        loadComponent: () => import('./features/knowledge/knowledge-page/knowledge-page').then((m) => m.KnowledgePage),
      },
      {
        path: 'settings/categories',
        loadComponent: () => import('./features/categories/categories-page/categories-page').then((m) => m.CategoriesPage),
      },
      {
        path: 'settings/tags',
        loadComponent: () => import('./features/tags/tags-page/tags-page').then((m) => m.TagsPage),
      },
      {
        path: 'categories',
        pathMatch: 'full',
        redirectTo: 'settings/categories',
      },
      {
        path: 'tags',
        pathMatch: 'full',
        redirectTo: 'settings/tags',
      },
      {
        path: 'projects',
        loadComponent: () => import('./features/projects/projects-page/projects-page').then((m) => m.ProjectsPage),
      },
      {
        path: 'projects/:id',
        loadComponent: () =>
          import('./features/projects/project-detail-page/project-detail-page').then(
            (m) => m.ProjectDetailPage,
          ),
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/profile/profile-page/profile-page').then((m) => m.ProfilePage),
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
