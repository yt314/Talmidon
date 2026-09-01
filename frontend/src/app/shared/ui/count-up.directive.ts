import { DestroyRef, Directive, ElementRef, effect, inject, input } from '@angular/core';

/**
 * מספר שמתגלגל מאפס לערכו כשהוא נכנס למסך. משמש במונים של דף הנחיתה.
 * הערך מוצג מיד, בלי אנימציה, כשהמשתמש ביקש פחות תנועה או כשאין ערך עדיין.
 */
@Directive({ selector: '[appCountUp]' })
export class CountUpDirective {
  readonly value = input.required<number | null>({ alias: 'appCountUp' });
  /** מוצמד לפני המספר, למשל '₪'. */
  readonly prefix = input('');
  readonly durationMs = input(1100);

  private readonly element = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
  private frame = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(this.frame));

    effect(() => {
      const target = this.value();
      // ביטול ריצה קודמת לפני כל דבר אחר: כשהערך מתעדכן באמצע אנימציה (הנתונים
      // מגיעים משתי קריאות שרת נפרדות), פריים תלוי־ועומד היה דורס את הערך החדש
      // וה-מונה היה נתקע על היעד הישן.
      cancelAnimationFrame(this.frame);

      if (target === null) {
        this.element.textContent = '—';
        return;
      }
      this.animate(target);
    });
  }

  private animate(target: number): void {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      this.render(target);
      return;
    }

    const duration = this.durationMs();
    const start = performance.now();
    const step = (now: number): void => {
      const progress = Math.min((now - start) / duration, 1);
      // ease-out: מהיר בהתחלה ונרגע לקראת הערך הסופי
      this.render(Math.round(target * (1 - Math.pow(1 - progress, 3))));
      if (progress < 1) this.frame = requestAnimationFrame(step);
    };
    this.frame = requestAnimationFrame(step);
  }

  private render(n: number): void {
    this.element.textContent = `${this.prefix()}${n.toLocaleString('he-IL')}`;
  }
}
