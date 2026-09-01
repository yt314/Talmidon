import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { ThemeService } from '../../core/theme/theme.service';

/** כפתור מעבר בין מצב בהיר לכהה — יושב בסרגל העליון של כל המעטפות. */
@Component({
  selector: 'app-theme-toggle',
  imports: [ButtonModule, TooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-button
      [icon]="theme.isDark() ? 'pi pi-sun' : 'pi pi-moon'"
      severity="secondary"
      [text]="true"
      [rounded]="true"
      styleClass="theme-toggle"
      [pTooltip]="theme.isDark() ? 'מצב בהיר' : 'מצב כהה'"
      tooltipPosition="bottom"
      [ariaLabel]="theme.isDark() ? 'מעבר למצב בהיר' : 'מעבר למצב כהה'"
      (onClick)="theme.toggle()" />
  `
})
export class ThemeToggleComponent {
  protected readonly theme = inject(ThemeService);
}
