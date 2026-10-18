import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';
import { ThemeToggleComponent } from '../../../shared/ui/theme-toggle.component';
import { UserMenuComponent } from '../../../shared/ui/user-menu.component';
import { AdminService } from '../admin.service';

@Component({
  selector: 'app-admin-shell',
  imports: [RouterLink, RouterOutlet, MenubarModule, ButtonModule, ToastModule, ConfirmDialogModule, ThemeToggleComponent, UserMenuComponent],
  templateUrl: './admin-shell.component.html'
})
export class AdminShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly admin = inject(AdminService);

  protected readonly email = this.auth.currentEmail;
  private readonly router = inject(Router);

  protected readonly menuItems = signal<MenuItem[]>(this.buildMenu(0));

  /**
   * מספר התקלות הפתוחות נטען פעם אחת בכניסה למסכי הניהול.
   *
   * יומן שצריך להיכנס אליו כדי לדעת שיש בו משהו נקרא פעם אחת ואז אף פעם —
   * בדיוק הכשל שבגללו נפילה של השרת התגלתה במקרה. מספר אדום ליד "תקלות" הוא
   * מה שהופך אותו למשהו שרואים בלי לחפש. כישלון בטעינה אינו מוצג: אי אפשר
   * להודיע על תקלות דרך הודעת שגיאה על טעינת התקלות.
   */
  ngOnInit(): void {
    this.admin.openErrorCount().subscribe({
      next: ({ open }) => this.menuItems.set(this.buildMenu(open)),
      error: () => undefined
    });
  }

  private buildMenu(openErrors: number): MenuItem[] {
    return [
      { label: 'מורות', icon: 'pi pi-users', routerLink: '/admin/teachers' },
      { label: 'תחומי לימוד', icon: 'pi pi-tags', routerLink: '/admin/subjects' },
      { label: 'הודעות מהאתר', icon: 'pi pi-comments', routerLink: '/admin/feedback' },
      {
        label: 'תקלות',
        icon: 'pi pi-exclamation-triangle',
        routerLink: '/admin/errors',
        badge: openErrors > 0 ? String(openErrors) : undefined,
        // אדום ולא צבע המותג: זו התראה, לא קישוט
        badgeStyleClass: 'p-badge-danger'
      }
    ];
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
