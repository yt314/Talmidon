import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** גוון הכרטיס — קובע את צבע האייקון ופס ההדגשה העליון. */
export type StatTone = 'primary' | 'accent' | 'success' | 'warn' | 'info';

/**
 * אריח מדד למסך הראשי — מספר גדול, תווית, אייקון, שורת משנה אופציונלית
 * ותג אופציונלי "דורש טיפול".
 * לחיץ כשמועבר ‎link‎, כך שכל אריח הוא קיצור דרך למסך הרלוונטי.
 */
@Component({
  selector: 'app-stat-card',
  imports: [RouterLink, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (link(); as target) {
      <a [routerLink]="target" class="stat-card" [class]="'stat-card-' + tone()">
        <ng-container [ngTemplateOutlet]="body" />
      </a>
    } @else {
      <div class="stat-card" [class]="'stat-card-' + tone()">
        <ng-container [ngTemplateOutlet]="body" />
      </div>
    }

    <ng-template #body>
      <span class="stat-card-icon"><i class="pi {{ icon() }}"></i></span>
      <span class="stat-card-label">{{ label() }}</span>
      <span class="stat-card-value" [class.stat-card-value-text]="variant() === 'text'">{{ value() ?? '—' }}</span>
      @if (hint(); as text) {
        <span class="stat-card-hint">{{ text }}</span>
      }
      @if (badge(); as text) {
        <span class="stat-card-badge">{{ text }}</span>
      }
    </ng-template>
  `
})
export class StatCardComponent {
  readonly label = input.required<string>();
  /** ‎null‎ מוצג כמקף — מצב טעינה, להבדיל מאפס אמיתי. */
  readonly value = input<string | number | null>(null);
  readonly icon = input('pi-chart-bar');
  readonly tone = input<StatTone>('primary');
  /**
   * ‎'metric'‎ הוא מספר גדול; ‎'text'‎ מיועד לתוכן מילולי כמו שיעורי בית,
   * שבגודל של מדד היה נשבר לשורות ונקרא כמו כותרת.
   */
  readonly variant = input<'metric' | 'text'>('metric');
  /** שורת משנה מתחת למספר — למשל שמות הילדים או שם התלמיד בשיעור הבא. */
  readonly hint = input<string | null>(null);
  readonly badge = input<string | null>(null);
  readonly link = input<string | unknown[] | null>(null);
}
