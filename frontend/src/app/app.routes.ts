import { Routes } from '@angular/router';
import { roleGuard } from './core/auth/auth.guard';
import { LoginComponent } from './features/auth/login/login.component';
import { RegisterComponent } from './features/auth/register/register.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./features/public/teacher-library/teacher-library.component').then(m => m.TeacherLibraryComponent)
  },
  {
    path: 'teachers/:id',
    loadComponent: () =>
      import('./features/public/teacher-profile/teacher-profile.component').then(m => m.TeacherProfileComponent)
  },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  {
    path: 'app',
    canActivate: [roleGuard(['Teacher'])],
    loadComponent: () => import('./features/teacher/teacher-shell/teacher-shell.component').then(m => m.TeacherShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardComponent },
      {
        path: 'students',
        loadComponent: () => import('./features/students/students-list/students-list.component').then(m => m.StudentsListComponent)
      },
      {
        path: 'students/:id',
        loadComponent: () => import('./features/students/student-detail/student-detail.component').then(m => m.StudentDetailComponent)
      },
      {
        path: 'lessons',
        loadComponent: () => import('./features/lessons/lessons-list/lessons-list.component').then(m => m.LessonsListComponent)
      },
      {
        path: 'payments',
        loadComponent: () => import('./features/payments/payments-list/payments-list.component').then(m => m.PaymentsListComponent)
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/teacher/profile/profile.component').then(m => m.TeacherProfileSettingsComponent)
      }
    ]
  },
  {
    path: 'parent',
    canActivate: [roleGuard(['Parent'])],
    loadComponent: () => import('./features/parent-portal/parent-shell/parent-shell.component').then(m => m.ParentShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'lessons' },
      {
        path: 'lessons',
        loadComponent: () => import('./features/parent-portal/parent-lessons/parent-lessons.component').then(m => m.ParentLessonsComponent)
      },
      {
        path: 'notes',
        loadComponent: () => import('./features/parent-portal/parent-notes/parent-notes.component').then(m => m.ParentNotesComponent)
      },
      {
        path: 'payments',
        loadComponent: () => import('./features/parent-portal/parent-payments/parent-payments.component').then(m => m.ParentPaymentsComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
