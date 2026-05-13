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
        path: 'knowledge',
        loadComponent: () => import('./features/knowledge/knowledge-page/knowledge-page').then((m) => m.KnowledgePage),
      },
      {
        path: 'categories',
        loadComponent: () => import('./features/categories/categories-page/categories-page').then((m) => m.CategoriesPage),
      },
      {
        path: 'tags',
        loadComponent: () => import('./features/tags/tags-page/tags-page').then((m) => m.TagsPage),
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
