import { Routes } from '@angular/router';
import { roleGuard } from './core/auth/auth.guard';
import { profileSetupGuard } from './core/auth/profile-setup.guard';
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
    title: 'פרופיל מורה',
    loadComponent: () =>
      import('./features/public/teacher-profile/teacher-profile.component').then(m => m.TeacherProfileComponent)
  },
  { path: 'login', title: 'התחברות', component: LoginComponent },
  { path: 'register', title: 'הרשמה כמורה', component: RegisterComponent },
  {
    path: 'forgot-password',
    title: 'שחזור סיסמה',
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },
  {
    path: 'set-password',
    title: 'קביעת סיסמה',
    loadComponent: () => import('./features/auth/set-password/set-password.component').then(m => m.SetPasswordComponent)
  },
  {
    path: 'app',
    canActivate: [roleGuard(['Teacher'])],
    canActivateChild: [profileSetupGuard],
    loadComponent: () => import('./features/teacher/teacher-shell/teacher-shell.component').then(m => m.TeacherShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      // מסך ההקמה עצמו מחוץ ל-profileSetupGuard, אחרת הוא היה מפנה אל עצמו בלולאה
      {
        path: 'setup',
        title: 'הקמת הפרופיל',
        loadComponent: () =>
          import('./features/teacher/profile-setup/profile-setup.component').then(m => m.ProfileSetupComponent)
      },
      { path: 'dashboard', title: 'ראשי', component: DashboardComponent },
      {
        path: 'students',
        title: 'תלמידים',
        loadComponent: () => import('./features/students/students-list/students-list.component').then(m => m.StudentsListComponent)
      },
      {
        path: 'students/:id',
        title: 'כרטיס תלמיד',
        loadComponent: () => import('./features/students/student-detail/student-detail.component').then(m => m.StudentDetailComponent)
      },
      {
        path: 'lessons',
        title: 'יומן שיעורים',
        loadComponent: () => import('./features/lessons/lessons-list/lessons-list.component').then(m => m.LessonsListComponent)
      },
      {
        path: 'payments',
        title: 'תשלומים',
        loadComponent: () => import('./features/payments/payments-list/payments-list.component').then(m => m.PaymentsListComponent)
      },
      {
        path: 'reports',
        title: 'דוחות',
        loadComponent: () => import('./features/reports/reports.component').then(m => m.ReportsComponent)
      },
      {
        path: 'contact-requests',
        title: 'פניות',
        loadComponent: () =>
          import('./features/contact-requests/contact-requests.component').then(m => m.ContactRequestsComponent)
      },
      {
        path: 'messages',
        title: 'הודעות',
        loadComponent: () => import('./features/messages/messages.component').then(m => m.MessagesComponent)
      },
      {
        path: 'lesson-plan',
        title: 'בניית מערך שיעור',
        loadComponent: () => import('./features/ai/lesson-plan.component').then(m => m.LessonPlanComponent)
      },
      {
        path: 'profile',
        title: 'הגדרות פרופיל',
        loadComponent: () => import('./features/teacher/profile/profile.component').then(m => m.TeacherProfileSettingsComponent)
      },
      {
        path: 'account',
        title: 'הגדרות חשבון',
        loadComponent: () =>
          import('./features/teacher/account-settings/account-settings.component').then(m => m.AccountSettingsComponent)
      }
    ]
  },
  {
    path: 'parent',
    canActivate: [roleGuard(['Parent'])],
    loadComponent: () => import('./features/parent-portal/parent-shell/parent-shell.component').then(m => m.ParentShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'ראשי',
        loadComponent: () =>
          import('./features/parent-portal/parent-dashboard/parent-dashboard.component').then(m => m.ParentDashboardComponent)
      },
      {
        path: 'lessons',
        title: 'יומן שיעורים',
        loadComponent: () => import('./features/parent-portal/parent-lessons/parent-lessons.component').then(m => m.ParentLessonsComponent)
      },
      {
        path: 'notes',
        title: 'הערות',
        loadComponent: () => import('./features/parent-portal/parent-notes/parent-notes.component').then(m => m.ParentNotesComponent)
      },
      {
        path: 'payments',
        title: 'תשלומים',
        loadComponent: () => import('./features/parent-portal/parent-payments/parent-payments.component').then(m => m.ParentPaymentsComponent)
      },
      {
        path: 'resources',
        title: 'חומרי לימוד',
        loadComponent: () =>
          import('./features/parent-portal/parent-resources/parent-resources.component').then(m => m.ParentResourcesComponent)
      },
      {
        path: 'messages',
        title: 'הודעות',
        loadComponent: () =>
          import('./features/messages/portal-messages.component').then(m => m.PortalMessagesComponent)
      }
    ]
  },
  {
    path: 'student',
    canActivate: [roleGuard(['Student'])],
    loadComponent: () => import('./features/student-portal/student-shell/student-shell.component').then(m => m.StudentShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'ראשי',
        loadComponent: () =>
          import('./features/student-portal/student-dashboard/student-dashboard.component').then(m => m.StudentDashboardComponent)
      },
      {
        path: 'lessons',
        title: 'יומן שיעורים',
        loadComponent: () => import('./features/student-portal/student-lessons/student-lessons.component').then(m => m.StudentLessonsComponent)
      },
      {
        path: 'notes',
        title: 'הערות',
        loadComponent: () => import('./features/student-portal/student-notes/student-notes.component').then(m => m.StudentNotesComponent)
      },
      {
        path: 'resources',
        title: 'חומרי לימוד',
        loadComponent: () =>
          import('./features/student-portal/student-resources/student-resources.component').then(m => m.StudentResourcesComponent)
      },
      {
        path: 'games',
        title: 'משחקים',
        loadComponent: () =>
          import('./features/student-portal/games/games.component').then(m => m.StudentGamesComponent)
      },
      {
        path: 'messages',
        title: 'הודעות',
        loadComponent: () =>
          import('./features/messages/portal-messages.component').then(m => m.PortalMessagesComponent)
      }
    ]
  },
  {
    path: 'admin',
    canActivate: [roleGuard(['Admin'])],
    loadComponent: () => import('./features/admin/admin-shell/admin-shell.component').then(m => m.AdminShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'teachers' },
      {
        path: 'teachers',
        title: 'מורות',
        loadComponent: () => import('./features/admin/admin-teachers/admin-teachers.component').then(m => m.AdminTeachersComponent)
      },
      {
        path: 'subjects',
        title: 'תחומי לימוד',
        loadComponent: () => import('./features/admin/admin-subjects/admin-subjects.component').then(m => m.AdminSubjectsComponent)
      },
      {
        path: 'feedback',
        title: 'הודעות מהאתר',
        loadComponent: () => import('./features/admin/admin-feedback/admin-feedback.component').then(m => m.AdminFeedbackComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
