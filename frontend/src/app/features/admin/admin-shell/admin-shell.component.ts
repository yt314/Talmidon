import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';
import { ThemeToggleComponent } from '../../../shared/ui/theme-toggle.component';

@Component({
  selector: 'app-admin-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule, ConfirmDialogModule, ThemeToggleComponent],
  templateUrl: './admin-shell.component.html'
})
export class AdminShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly menuItems: MenuItem[] = [{ label: 'מורות', icon: 'pi pi-users', routerLink: '/admin/teachers' }];

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
