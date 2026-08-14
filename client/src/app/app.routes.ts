import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

// Every feature is loaded on demand. It costs nothing to write and means the login screen
// does not ship the employee and team screens to someone who has not signed in yet.
export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in — Permit To Work',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: 'employees',
    title: 'Employees — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/employees/employee-list').then((m) => m.EmployeeList),
  },
  {
    path: 'employees/:id',
    title: 'Employee — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/employees/employee-detail').then((m) => m.EmployeeDetail),
  },
  {
    path: 'teams',
    title: 'Teams — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/teams/team-list').then((m) => m.TeamList),
  },
  {
    path: 'teams/:id',
    title: 'Team — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/teams/team-detail').then((m) => m.TeamDetail),
  },
  {
    path: 'settings',
    title: 'Settings — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/settings/settings').then((m) => m.Settings),
  },
  { path: '', pathMatch: 'full', redirectTo: 'teams' },
  { path: '**', redirectTo: 'teams' },
];
