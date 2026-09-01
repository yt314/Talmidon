import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * כותרת מסך אחידה — כותרת, שורת הסבר, ואזור פעולות בצד. מחליפה את ה-‎<h1>‎ החשוף
 * שהיה בראש כל מסך, כך שכל המסכים מתחילים באותו מרווח ובאותה היררכיה.
 */
@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div class="page-header-text">
        <h1 class="page-header-title">
          @if (icon()) {
            <i class="pi {{ icon() }} page-header-icon"></i>
          }
          {{ title() }}
        </h1>
        @if (subtitle()) {
          <p class="page-header-subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="page-header-actions"><ng-content /></div>
    </header>
  `
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly icon = input<string | null>(null);
}
