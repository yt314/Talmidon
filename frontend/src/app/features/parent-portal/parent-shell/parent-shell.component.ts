import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-parent-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule],
  templateUrl: './parent-shell.component.html'
})
export class ParentShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = this.auth.currentEmail;

  protected readonly menuItems: MenuItem[] = [
    { label: 'יומן', icon: 'pi pi-calendar', routerLink: '/parent/lessons' },
    { label: 'הערות', icon: 'pi pi-book', routerLink: '/parent/notes' },
    { label: 'תשלומים', icon: 'pi pi-wallet', routerLink: '/parent/payments' }
  ];

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
