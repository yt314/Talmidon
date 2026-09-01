import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * מצב ריק אחיד — אייקון, כותרת, משפט הסבר, ומקום לכפתור פעולה.
 * מחליף את שורות ה-"אין X עדיין" שהיו מפוזרות בכל מסך בניסוח וסגנון שונים.
 */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty-state">
      <span class="empty-state-icon"><i class="pi {{ icon() }}"></i></span>
      <p class="empty-state-title">{{ title() }}</p>
      @if (hint()) {
        <p class="empty-state-hint">{{ hint() }}</p>
      }
      <div class="empty-state-action"><ng-content /></div>
    </div>
  `
})
export class EmptyStateComponent {
  readonly icon = input('pi-inbox');
  readonly title = input.required<string>();
  readonly hint = input<string | null>(null);
}
