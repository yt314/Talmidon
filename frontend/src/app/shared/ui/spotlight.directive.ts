import { Directive, ElementRef, inject } from '@angular/core';

/**
 * הילה רכה שעוקבת אחרי הסמן על גבי הכרטיס. הכיוון נשמר בשני משתני CSS
 * (‎--spot-x/--spot-y‎) וה-CSS מצייר את ההילה — כאן רק מודדים.
 *
 * ‎pointermove‎ ולא ‎mousemove‎, כדי שגם עט/מגע יעבדו; במכשירי מגע ה-CSS ממילא
 * מכבה את האפקט (‎@media (hover: hover)‎), אז אין כאן "הילה תקועה" אחרי נגיעה.
 */
@Directive({
  selector: '[appSpotlight]',
  host: {
    '[class.spotlight]': 'true',
    '(pointermove)': 'track($event)',
    '(pointerleave)': 'clear()'
  }
})
export class SpotlightDirective {
  private readonly element = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;

  protected track(event: PointerEvent): void {
    const rect = this.element.getBoundingClientRect();
    this.element.style.setProperty('--spot-x', `${event.clientX - rect.left}px`);
    this.element.style.setProperty('--spot-y', `${event.clientY - rect.top}px`);
    this.element.style.setProperty('--spot-opacity', '1');
  }

  protected clear(): void {
    this.element.style.setProperty('--spot-opacity', '0');
  }
}
