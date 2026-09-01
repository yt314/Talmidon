import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';
import { ThemeToggleComponent } from './theme-toggle.component';

/**
 * מעטפת משותפת לארבעת מסכי האימות. פאנל מיתוג בצד אחד והטופס בצד השני; מתחת
 * ל-900px הפאנל נעלם ונשאר הטופס בלבד, כדי לא לדחוף אותו מתחת לקיפול בנייד.
 *
 * הטופס עצמו מגיע כתוכן מוקרן (ng-content), כך שכל מסך שומר על הלוגיקה שלו
 * והמעטפת אחראית רק על הפריסה, המיתוג וכפתור המצב הכהה.
 */
@Component({
  selector: 'app-auth-layout',
  imports: [RouterLink, CardModule, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="auth-page">
      <div class="auth-shell">
        <aside class="auth-brand aurora dot-grid">
          <div class="auth-brand-inner">
            <a routerLink="/" class="brand auth-brand-logo">
              <span class="brand-mark"><i class="pi pi-graduation-cap"></i></span>
              תלמידון
            </a>
            <h2>{{ headline() }}</h2>
            <ul class="auth-brand-points">
              @for (point of points(); track point) {
                <li><i class="pi pi-check"></i> {{ point }}</li>
              }
            </ul>
          </div>
        </aside>

        <div class="auth-form-side">
          <div class="auth-form-top">
            <a routerLink="/" class="brand auth-mobile-brand">
              <span class="brand-mark"><i class="pi pi-graduation-cap"></i></span>
              תלמידון
            </a>
            <app-theme-toggle />
          </div>

          <p-card styleClass="auth-card">
            <ng-content />
          </p-card>

          <a routerLink="/" class="auth-back">
            <i class="pi pi-arrow-right text-xs"></i> חזרה לספריית המורות
          </a>
        </div>
      </div>
    </div>
  `
})
export class AuthLayoutComponent {
  readonly headline = input('ניהול שיעורים פרטיים, במקום אחד');
  readonly points = input<readonly string[]>([
    'יומן שיעורים עם בקשות ואישורים',
    'מעקב פדגוגי וחומרי לימוד לתלמיד',
    'חיוב לפי שיעור, בלי מנויים'
  ]);
}
