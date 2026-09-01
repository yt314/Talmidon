import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';
import { ThemeToggleComponent } from '../../../shared/ui/theme-toggle.component';
import { Gender } from '../../../core/models/gender';
import { StudentPortalService } from '../student-portal.service';
import { UserMenuComponent } from '../../../shared/ui/user-menu.component';

@Component({
  selector: 'app-student-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule, ThemeToggleComponent, UserMenuComponent],
  templateUrl: './student-shell.component.html'
})
export class StudentShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly portalService = inject(StudentPortalService);

  protected readonly fullName = signal<string | null>(null);
  private readonly gender = signal<Gender | null>(null);

  protected readonly roleLabel = computed(() =>
    this.gender() === Gender.Male ? 'תלמיד' : this.gender() === Gender.Female ? 'תלמידה' : 'תלמיד/ה'
  );

  protected readonly menuItems: MenuItem[] = [
    { label: 'ראשי', icon: 'pi pi-home', routerLink: '/student/dashboard' },
    { label: 'יומן', icon: 'pi pi-calendar', routerLink: '/student/lessons' },
    { label: 'הערות', icon: 'pi pi-book', routerLink: '/student/notes' },
    { label: 'חומרי לימוד', icon: 'pi pi-folder-open', routerLink: '/student/resources' }
  ];

  ngOnInit(): void {
    this.portalService.myProfile().subscribe(profile => {
      this.fullName.set(profile.fullName);
      this.gender.set(profile.gender);
    });
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
