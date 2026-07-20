import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';
import { Gender } from '../../../core/models/gender';
import { StudentPortalService } from '../student-portal.service';

@Component({
  selector: 'app-student-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule],
  templateUrl: './student-shell.component.html'
})
export class StudentShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly portalService = inject(StudentPortalService);

  private readonly fullName = signal<string | null>(null);
  private readonly gender = signal<Gender | null>(null);

  protected readonly greeting = computed(() => {
    const label = this.gender() === Gender.Male ? 'התלמיד' : this.gender() === Gender.Female ? 'התלמידה' : 'התלמיד/ה';
    return this.fullName() ? `שלום, ${label} ${this.fullName()}` : `שלום, ${label}`;
  });

  protected readonly menuItems: MenuItem[] = [
    { label: 'יומן', icon: 'pi pi-calendar', routerLink: '/student/lessons' },
    { label: 'הערות', icon: 'pi pi-book', routerLink: '/student/notes' }
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
