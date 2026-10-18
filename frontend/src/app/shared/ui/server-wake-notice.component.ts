import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ServerWakeService } from '../../core/http/server-wake.service';

/**
 * רצועה קטנה בראש המסך כשהשרת לוקח זמן לענות.
 *
 * יושבת ב-app-root כמו תג ההרצה ואזהרת ההתנתקות, כדי שתופיע בכל מסך — כולל
 * הספרייה הציבורית וטופס הפנייה, שהם בדיוק המקומות שבהם מבקר חדש פוגש שרת ישן.
 *
 * למטה במרכז, ולא למעלה: בראש המסך היא כיסתה בטלפון את הלוגו ואת כפתור
 * ההתחברות. במסך צר היא מורמת מעט כדי לא לשבת על תג ההרצה שבפינה השמאלית.
 */
@Component({
  selector: 'app-server-wake-notice',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (waking()) {
      <div class="wake-notice" role="status" aria-live="polite">
        <i class="pi pi-spin pi-spinner" aria-hidden="true"></i>
        <span>השרת מתעורר. זה יכול לקחת עד דקה.</span>
      </div>
    }
  `,
  styles: [
    `
      .wake-notice {
        position: fixed;
        bottom: 1rem;
        left: 50%;
        transform: translateX(-50%);
        z-index: 1100;
        display: flex;
        align-items: center;
        gap: 0.5rem;
        max-width: calc(100vw - 2rem);
        padding: 0.5rem 0.9rem;
        border: 1px solid var(--p-content-border-color);
        border-radius: 999px;
        background: var(--p-content-background);
        color: var(--p-text-color);
        font-size: 0.8125rem;
        box-shadow: 0 4px 14px rgb(0 0 0 / 0.14);
      }
      /* בטלפון תג ההרצה רחב יחסית למסך — מרימים את ההודעה מעליו */
      @media (max-width: 640px) {
        .wake-notice {
          bottom: 3.75rem;
        }
      }
      @media print {
        .wake-notice {
          display: none;
        }
      }
    `
  ]
})
export class ServerWakeNoticeComponent {
  protected readonly waking = inject(ServerWakeService).waking;
}
