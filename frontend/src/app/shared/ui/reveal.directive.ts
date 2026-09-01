import { DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';

/**
 * חושף אלמנט בגלילה — הוא מתחיל שקוף ומוזז מעט, ועולה למקומו כשהוא נכנס למסך.
 * מבוסס IntersectionObserver, בלי שום ספרייה.
 *
 * שני דברים שנועדו למנוע תקלות נפוצות בדפוס הזה:
 *   1. מי שביקש פחות תנועה (prefers-reduced-motion) מקבל את התוכן גלוי מיד — בלי
 *      אנימציה ובלי observer בכלל.
 *   2. הצפייה מפסיקה אחרי החשיפה הראשונה, כך שהאלמנט לא "מהבהב" בגלילה חזרה.
 */
@Directive({
  selector: '[appReveal]',
  host: { '[class.reveal]': 'true', '[style.--reveal-delay]': 'delay()' }
})
export class RevealDirective {
  /** השהיה לפני החשיפה, לאפקט מדורג בין אחים. למשל '0.1s'. */
  readonly delay = input('0s', { alias: 'appReveal' });

  constructor() {
    const element = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      element.classList.add('reveal-visible');
      return;
    }

    const observer = new IntersectionObserver(
      entries => {
        for (const entry of entries) {
          if (!entry.isIntersecting) continue;
          entry.target.classList.add('reveal-visible');
          observer.unobserve(entry.target);
        }
      },
      // חושפים כשהאלמנט כבר נכנס מספיק פנימה, לא ברגע שקצהו נוגע במסך
      { threshold: 0.15, rootMargin: '0px 0px -60px 0px' }
    );

    observer.observe(element);
    inject(DestroyRef).onDestroy(() => observer.disconnect());
  }
}
