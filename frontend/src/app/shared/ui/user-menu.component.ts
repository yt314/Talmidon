import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { MenuModule } from 'primeng/menu';
import { getAvatarColor, getInitials } from '../avatar/avatar.util';

/**
 * תפריט המשתמש בסרגל העליון — עיגול ראשי-תיבות שפותח תפריט עם השם המלא וההתנתקות.
 *
 * למה תפריט ולא ברכה + כפתור גלויים: הברכה והכפתור תפסו רוחב שגדל עם אורך השם,
 * ובשמות ארוכים סרגל התפריט נשבר לשתי שורות. כאן הרוחב קבוע — עיגול אחד — ולכן
 * הסרגל נשאר בשורה אחת בכל אורך שם.
 */
@Component({
  selector: 'app-user-menu',
  imports: [MenuModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="user-menu-trigger"
      [attr.aria-label]="greeting()"
      [title]="greeting()"
      (click)="menu.toggle($event)">
      <span class="avatar-circle user-menu-avatar" [style.--avatar-color]="color()">{{ initials() }}</span>
      <i class="pi pi-angle-down text-xs"></i>
    </button>

    <p-menu #menu [model]="items()" [popup]="true" [appendTo]="'body'" styleClass="user-menu-panel">
      <ng-template pTemplate="start">
        <div class="user-menu-header">
          <span class="avatar-circle" [style.--avatar-color]="color()">{{ initials() }}</span>
          <div class="user-menu-header-text">
            <span class="user-menu-name">{{ name() || '—' }}</span>
            <span class="user-menu-role">{{ roleLabel() }}</span>
          </div>
        </div>
      </ng-template>
    </p-menu>
  `
})
export class UserMenuComponent {
  readonly name = input<string | null>(null);
  /** התיאור מתחת לשם — "מורה", "הורה" וכו'. */
  readonly roleLabel = input('');
  /** נקרא כשנבחרת ההתנתקות. */
  readonly logout = output<void>();

  protected readonly initials = computed(() => (this.name() ? getInitials(this.name()!) : '?'));
  protected readonly color = computed(() => getAvatarColor(this.name() ?? ''));
  protected readonly greeting = computed(() => (this.name() ? `${this.name()} — תפריט משתמש` : 'תפריט משתמש'));

  protected readonly items = computed<MenuItem[]>(() => [
    { separator: true },
    { label: 'התנתקות', icon: 'pi pi-sign-out', command: () => this.logout.emit() }
  ]);
}
