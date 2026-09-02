import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';
import { ThemeToggleComponent } from '../../../shared/ui/theme-toggle.component';
import { Gender } from '../../../core/models/gender';
import { ParentsService } from '../../parents/parents.service';
import { ParentPortalService } from '../parent-portal.service';
import { UserMenuComponent } from '../../../shared/ui/user-menu.component';

@Component({
  selector: 'app-parent-shell',
  imports: [RouterLink, RouterOutlet, MenubarModule, ButtonModule, ToastModule, ThemeToggleComponent, UserMenuComponent],
  templateUrl: './parent-shell.component.html'
})
export class ParentShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly parentsService = inject(ParentsService);
  private readonly portalService = inject(ParentPortalService);

  protected readonly fullName = signal<string | null>(null);
  private readonly gender = signal<Gender | null>(null);
  private readonly childrenNames = signal<string[]>([]);

  /** "אמא של רותם, איתי" — התיאור מתחת לשם בתפריט המשתמש. */
  protected readonly roleLabel = computed(() => {
    const relation = this.gender() === Gender.Male ? 'אבא' : this.gender() === Gender.Female ? 'אמא' : 'הורה';
    const names = this.childrenNames();
    return names.length > 0 ? `${relation} של ${names.join(', ')}` : relation;
  });

  protected readonly menuItems: MenuItem[] = [
    { label: 'ראשי', icon: 'pi pi-home', routerLink: '/parent/dashboard' },
    { label: 'יומן', icon: 'pi pi-calendar', routerLink: '/parent/lessons' },
    { label: 'הערות', icon: 'pi pi-book', routerLink: '/parent/notes' },
    { label: 'חומרי לימוד', icon: 'pi pi-folder-open', routerLink: '/parent/resources' },
    { label: 'תשלומים', icon: 'pi pi-wallet', routerLink: '/parent/payments' }
  ];

  ngOnInit(): void {
    this.parentsService.myProfile().subscribe(profile => {
      this.fullName.set(profile.fullName);
      this.gender.set(profile.gender);
    });
    this.portalService.myChildren().subscribe(children => this.childrenNames.set(children.map(c => c.fullName)));
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
