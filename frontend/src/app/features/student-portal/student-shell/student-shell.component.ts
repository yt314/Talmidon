import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-student-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule],
  templateUrl: './student-shell.component.html'
})
export class StudentShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = this.auth.currentEmail;

  protected readonly menuItems: MenuItem[] = [
    { label: 'יומן', icon: 'pi pi-calendar', routerLink: '/student/lessons' },
    { label: 'הערות', icon: 'pi pi-book', routerLink: '/student/notes' }
  ];

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
