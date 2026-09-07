import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../../core/auth/auth.service';
import { ProfileSetupService } from '../profile-setup/profile-setup.service';
import { ThemeToggleComponent } from '../../../shared/ui/theme-toggle.component';
import { NotificationsBellComponent } from '../../notifications/notifications-bell/notifications-bell.component';
import { TeacherProfileService } from '../profile/profile.service';
import { UserMenuComponent } from '../../../shared/ui/user-menu.component';
import { QuickSearchComponent } from '../../../shared/ui/quick-search.component';
@Component({
  selector: 'app-teacher-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule, ConfirmDialogModule, NotificationsBellComponent, ThemeToggleComponent, UserMenuComponent, RouterLink, QuickSearchComponent, TooltipModule],
  templateUrl: './teacher-shell.component.html'
})
export class TeacherShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly profileSetup = inject(ProfileSetupService);

  /** כתובת הניווט הנוכחית, כדי שהבאנר לא יופיע במסך שאליו הוא מפנה. */
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects)
    ),
    { initialValue: this.router.url }
  );

  /**
   * תזכורת להשלמת הפרופיל, גם למי שבחרה "אמלא אחר כך".
   *
   * לא במסך ההקמה עצמו: שם הכפתור שבה הפנה למסך שכבר פתוח. ולא אחרי סגירה
   * ידנית — היא חוזרת בכניסה הבאה, כך שהיא נשארת תזכורת ולא הופכת לרעש קבוע.
   */
  protected readonly needsSetup = computed(
    () =>
      this.profileSetup.needsSetup() &&
      !this.profileSetup.bannerHidden() &&
      !this.url().startsWith('/app/setup')
  );
  private readonly profileService = inject(TeacherProfileService);

  protected readonly fullName = signal<string | null>(null);

  protected readonly menuItems: MenuItem[] = [
    { label: 'ראשי', icon: 'pi pi-home', routerLink: '/app/dashboard' },
    { label: 'תלמידים', icon: 'pi pi-users', routerLink: '/app/students' },
    { label: 'יומן', icon: 'pi pi-calendar', routerLink: '/app/lessons' },
    { label: 'תשלומים', icon: 'pi pi-wallet', routerLink: '/app/payments' },
    { label: 'דוחות', icon: 'pi pi-chart-bar', routerLink: '/app/reports' },
    { label: 'פניות', icon: 'pi pi-inbox', routerLink: '/app/contact-requests' },
    { label: 'ספריית המורות', icon: 'pi pi-book', routerLink: '/' },
    // שתי מסכי ההגדרות מקובצים לתפריט משנה — שבעה פריטים ברצף שברו את הסרגל
    // לשתי שורות על מסך רגיל.
    {
      label: 'הגדרות',
      icon: 'pi pi-cog',
      items: [
        { label: 'פרופיל ציבורי', icon: 'pi pi-id-card', routerLink: '/app/profile' },
        { label: 'חשבון וסיסמה', icon: 'pi pi-lock', routerLink: '/app/account' }
      ]
    }
  ];

  ngOnInit(): void {
    this.profileService.getMyProfile().subscribe(profile => this.fullName.set(profile.fullName));
  }

  protected dismissSetupBanner(): void {
    this.profileSetup.dismissBanner();
  }

  logout(): void {
    this.profileSetup.reset();
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
