import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';
import { TeacherProfileService } from '../profile/profile.service';

@Component({
  selector: 'app-teacher-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule, ConfirmDialogModule],
  templateUrl: './teacher-shell.component.html'
})
export class TeacherShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly profileService = inject(TeacherProfileService);

  private readonly fullName = signal<string | null>(null);
  protected readonly greeting = computed(() => (this.fullName() ? `שלום, המורה ${this.fullName()}` : 'שלום, המורה'));

  protected readonly menuItems: MenuItem[] = [
    { label: 'ראשי', icon: 'pi pi-home', routerLink: '/app/dashboard' },
    { label: 'תלמידים', icon: 'pi pi-users', routerLink: '/app/students' },
    { label: 'יומן שיעורים', icon: 'pi pi-calendar', routerLink: '/app/lessons' },
    { label: 'תשלומים', icon: 'pi pi-wallet', routerLink: '/app/payments' },
    { label: 'הגדרות פרופיל', icon: 'pi pi-cog', routerLink: '/app/profile' }
  ];

  ngOnInit(): void {
    this.profileService.getMyProfile().subscribe(profile => this.fullName.set(profile.fullName));
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
