import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/auth.guard';
import { Roles } from './core/auth/auth.service';

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
    path: 'permits',
    title: 'Permits — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/permits/permit-list').then((m) => m.PermitList),
  },
  {
    path: 'my-permits',
    title: 'My permits — Permit To Work',
    // No role guard: everyone has permits they are on, including administrators.
    canActivate: [authGuard],
    loadComponent: () => import('./features/permits/my-permits').then((m) => m.MyPermits),
  },
  {
    path: 'active-permits',
    title: 'Active permits — Permit To Work',
    // Asking where a named colleague is working is a different privilege from seeing your
    // own assignments. The API enforces the same list independently.
    canActivate: [
      authGuard,
      roleGuard(Roles.Administrator, Roles.Supervisor, Roles.SafetyOfficer, Roles.Responsible),
    ],
    loadComponent: () => import('./features/permits/crew-search').then((m) => m.CrewSearch),
  },
  {
    // Before 'permits/:id', or "new" is read as an id and the detail page 404s.
    path: 'permits/new',
    title: 'Raise a permit — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/permits/permit-form').then((m) => m.PermitForm),
  },
  {
    path: 'permits/:id/edit',
    title: 'Edit permit — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/permits/permit-form').then((m) => m.PermitForm),
  },
  {
    path: 'permits/:id',
    title: 'Permit — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/permits/permit-detail').then((m) => m.PermitDetail),
  },
  {
    path: 'approval-panels',
    title: 'Approval panels — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/approvals/approval-panels').then((m) => m.ApprovalPanels),
  },
  {
    path: 'admin/reference-data',
    title: 'Reference data — Permit To Work',
    // Two guards, and both matter: authGuard sends a signed-out visitor to the login page,
    // roleGuard turns anybody else away. The API refuses independently — this only avoids
    // offering a screen that could not work.
    canActivate: [authGuard, roleGuard(Roles.Administrator)],
    loadComponent: () => import('./features/admin/reference-data').then((m) => m.ReferenceDataAdmin),
  },
  {
    path: 'admin/audit',
    title: 'Audit log — Permit To Work',
    canActivate: [authGuard, roleGuard(Roles.Administrator)],
    loadComponent: () => import('./features/admin/audit-log').then((m) => m.AuditLog),
  },
  {
    path: 'profile',
    title: 'My profile — Permit To Work',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
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
