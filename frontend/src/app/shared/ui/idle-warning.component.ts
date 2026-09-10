import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { IdleLogoutService } from '../../core/auth/idle-logout.service';

/**
 * אזהרה לפני התנתקות אוטומטית.
 *
 * מוצגת ולא מנתקת בשקט: מסך שנעלם באמצע עבודה בלי הסבר נראה כמו תקלה, ומי שקרה לה
 * את זה פעם אחת מפסיקה לסמוך על המערכת.
 */
@Component({
  selector: 'app-idle-warning',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DialogModule, ButtonModule],
  template: `
    <p-dialog
      [visible]="idle.warning()"
      [modal]="true"
      [closable]="false"
      [draggable]="false"
      header="עוד רגע מתנתקים"
      [style]="{ width: '26rem', maxWidth: '92vw' }"
      appendTo="body">
      <p class="m-0 mb-3">
        לא הייתה פעילות בחשבון זמן מה, ומטעמי אבטחה נתנתק בעוד
        <strong>{{ idle.secondsLeft() }}</strong> שניות.
      </p>
      <ng-template pTemplate="footer">
        <p-button label="התנתקות" severity="secondary" [text]="true" (onClick)="idle.logoutNow()" />
        <p-button label="אני כאן, להישאר מחוברת" icon="pi pi-check" (onClick)="idle.keepAlive()" />
      </ng-template>
    </p-dialog>
  `
})
export class IdleWarningComponent {
  protected readonly idle = inject(IdleLogoutService);
}
