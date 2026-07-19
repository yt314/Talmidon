import { Component, inject } from '@angular/core';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-dashboard',
  imports: [CardModule, TagModule],
  template: `
    <p-card>
      <h2 class="mt-0">שלום 👋</h2>
      <p class="text-lg">מחוברת כ: <strong>{{ email() }}</strong></p>
      <div class="flex gap-2 flex-wrap">
        @for (role of roles(); track role) {
          <p-tag [value]="role" severity="info" />
        }
      </div>
      <p class="text-color-secondary mt-4">
        זהו מסך זמני. בשלבים הבאים ייבנו כאן סיכום שיעורי היום, בקשות ממתינות ותשלומים פתוחים.
      </p>
    </p-card>
  `
})
export class DashboardComponent {
  private readonly auth = inject(AuthService);

  protected readonly email = this.auth.currentEmail;
  protected readonly roles = this.auth.roles;
}
