import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-teacher-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule, ConfirmDialogModule],
  templateUrl: './teacher-shell.component.html'
})
export class TeacherShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = this.auth.currentEmail;

  protected readonly menuItems: MenuItem[] = [
    { label: 'ראשי', icon: 'pi pi-home', routerLink: '/app/dashboard' },
    { label: 'תלמידים', icon: 'pi pi-users', routerLink: '/app/students' },
    { label: 'יומן שיעורים', icon: 'pi pi-calendar', routerLink: '/app/lessons' }
  ];

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
