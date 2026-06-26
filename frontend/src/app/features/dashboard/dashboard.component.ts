import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-dashboard',
  imports: [CardModule, ButtonModule, TagModule],
  template: `
    <div class="p-4">
      <div class="flex align-items-center justify-content-between mb-4">
        <h1 class="m-0 text-2xl font-bold">
          <i class="pi pi-graduation-cap mr-2" style="color: var(--p-primary-color)"></i>
          תלמידון
        </h1>
        <p-button label="התנתקות" icon="pi pi-sign-out" severity="secondary" [outlined]="true" (onClick)="logout()" />
      </div>

      <p-card>
        <h2 class="mt-0">שלום 👋</h2>
        <p class="text-lg">מחוברת כ: <strong>{{ email() }}</strong></p>
        <div class="flex gap-2 flex-wrap">
          @for (role of roles(); track role) {
            <p-tag [value]="role" severity="info" />
          }
        </div>
        <p class="text-color-secondary mt-4">
          זהו מסך זמני. בשלבים הבאים ייבנו כאן ניהול התלמידים, היומן והתשלומים.
        </p>
      </p-card>
    </div>
  `
})
export class DashboardComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = this.auth.currentEmail;
  protected readonly roles = this.auth.roles;

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
